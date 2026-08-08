using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// A transaction: an <see cref="ITransactionMessage"/> (legacy <see cref="Message"/> or <see cref="MessageV0"/>)
/// plus one signature slot per required signer. Sign it with <see cref="Sign"/>, then serialize with
/// <see cref="Serialize()"/> or <see cref="ToBase64"/> to submit it.
/// </summary>
public sealed class Transaction
{
    /// <summary>The length of an Ed25519 signature in bytes (64).</summary>
    public const int SignatureLength = 64;

    private readonly byte[][] _signatures;
    private byte[]? _signedMessageBytes;
    private PublicKey[]? _signedRequiredSignerKeys;

    private Transaction(ITransactionMessage message)
    {
        Message = message;
        _signatures = new byte[message.RequiredSignatures][];
        for (var i = 0; i < _signatures.Length; i++)
            _signatures[i] = new byte[SignatureLength];
    }

    private Transaction(ITransactionMessage message, byte[][] signatures, byte[] signedMessageBytes)
    {
        Message = message;
        _signatures = signatures;
        _signedMessageBytes = signedMessageBytes;
        _signedRequiredSignerKeys = CopyRequiredSignerKeys(message);
    }

    /// <summary>
    /// The message being signed and sent. After the transaction is successfully signed or deserialized,
    /// serialization continues to use the captured message bytes even if this object graph is later mutated.
    /// </summary>
    public ITransactionMessage Message { get; }

    /// <summary>Creates an unsigned transaction for <paramref name="message"/>, with every signature slot zeroed.</summary>
    /// <param name="message">The compiled message.</param>
    /// <returns>The unsigned transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    public static Transaction Create(ITransactionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new Transaction(message);
    }

    /// <summary>
    /// Parses a transaction from its wire bytes: the signatures followed by a legacy or v0 message, retaining
    /// the exact parsed message bytes for stable reserialization.
    /// </summary>
    /// <param name="data">The serialized transaction.</param>
    /// <returns>The parsed transaction, carrying its signatures.</returns>
    /// <exception cref="FormatException">
    /// The data is truncated, contains trailing bytes, has a malformed compact-u16 length, or the message is invalid or breaks
    /// one of Solana's sanitize rules, or the signature count does not match the message's required
    /// signatures.
    /// </exception>
    public static Transaction Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            var offset = 0;
            var signatureCount = ShortVec.Decode(data[offset..], out var read);
            offset += read;

            var signatures = new byte[signatureCount][];
            for (var i = 0; i < signatureCount; i++)
            {
                signatures[i] = data.Slice(offset, SignatureLength).ToArray();
                offset += SignatureLength;
            }

            var messageBytes = data[offset..];
            ITransactionMessage message = messageBytes.Length > 0 && (messageBytes[0] & MessageV0.VersionPrefix) != 0
                ? MessageV0.Deserialize(messageBytes)
                : global::SolSharp.Programs.Message.Deserialize(messageBytes);

            // Solana's sanitize step requires exactly one signature slot per required signer (a partially
            // signed transaction carries zeroed slots, never fewer). Accepting a mismatch here would let
            // Sign index past the slot array and surface as an unrelated IndexOutOfRangeException.
            if (signatureCount != message.RequiredSignatures)
                throw new FormatException(
                    $"The transaction carries {signatureCount} signature slot(s) but its message requires {message.RequiredSignatures}.");

            return new Transaction(message, signatures, messageBytes.ToArray());
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Span indexing and slicing throw index errors on short input; surface the documented type.
            throw new FormatException("The transaction data is truncated.", exception);
        }
    }

    /// <summary>
    /// Signs the message with each signer, placing each signature in the slot matching the signer's position
    /// among the required signers. The first successful non-empty call captures the signed message bytes;
    /// later mutations to <see cref="Message"/> do not change serialization or the bytes passed to another signer.
    /// </summary>
    /// <param name="signers">The signers to apply; each must be a required signer of the message.</param>
    /// <returns>This transaction, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="signers"/> or one of its elements is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A signer is not one of the message's required signers or returns a signature whose length is not
    /// <see cref="SignatureLength"/> bytes.
    /// </exception>
    public Transaction Sign(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var requiredSignerKeys = _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message);
        var pending = new (int Index, byte[] Signature)[signers.Length];
        for (var i = 0; i < signers.Length; i++)
        {
            var signer = signers[i];
            ArgumentNullException.ThrowIfNull(signer, nameof(signers));

            var index = RequiredSignerIndex(requiredSignerKeys, signer.PublicKey);
            if (index < 0)
                throw new ArgumentException($"{signer.PublicKey} is not a required signer of this transaction.", nameof(signers));

            pending[i].Index = index;
        }

        var message = _signedMessageBytes ?? Message.Serialize();
        for (var i = 0; i < signers.Length; i++)
        {
            var signer = signers[i];
            var signature = signer.Sign(message);
            if (signature is null || signature.Length != SignatureLength)
                throw new ArgumentException(
                    $"A signer must return a {SignatureLength}-byte Ed25519 signature, got {signature?.Length ?? 0} bytes.",
                    nameof(signers));

            pending[i].Signature = [.. signature];
        }

        for (var i = 0; i < pending.Length; i++)
            _signatures[pending[i].Index] = pending[i].Signature;

        if (signers.Length > 0)
        {
            _signedMessageBytes ??= message;
            _signedRequiredSignerKeys ??= requiredSignerKeys;
        }

        return this;
    }

    /// <summary>Serializes the transaction to its wire bytes: the signatures followed by the message.</summary>
    /// <returns>The serialized transaction.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public byte[] Serialize()
    {
        var buffer = new byte[GetSerializedLength()];
        TrySerialize(buffer, out _);
        return buffer;
    }

    /// <summary>Returns the exact length of the serialized transaction, in bytes.</summary>
    /// <returns>The serialized length.</returns>
    public int GetSerializedLength()
        => ShortVec.GetByteCount(_signatures.Length)
           + _signatures.Length * SignatureLength
           + (_signedMessageBytes?.Length ?? Message.GetSerializedLength());

    /// <summary>
    /// Serializes the transaction into <paramref name="destination"/> without allocating - the hot-path
    /// alternative to <see cref="Serialize()"/> for latency-sensitive senders.
    /// </summary>
    /// <param name="destination">The span to write into.</param>
    /// <param name="written">The number of bytes written; <c>0</c> when the span is too small.</param>
    /// <returns><c>false</c> when <paramref name="destination"/> is smaller than <see cref="GetSerializedLength"/> bytes.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public bool TrySerialize(Span<byte> destination, out int written)
    {
        if (destination.Length < GetSerializedLength())
        {
            written = 0;
            return false;
        }

        var offset = ShortVec.Encode(_signatures.Length, destination);
        foreach (var signature in _signatures)
        {
            signature.CopyTo(destination[offset..]);
            offset += SignatureLength;
        }

        if (_signedMessageBytes is { } signedMessage)
        {
            signedMessage.CopyTo(destination[offset..]);
            offset += signedMessage.Length;
        }
        else
        {
            offset += Message.Serialize(destination[offset..]);
        }
        written = offset;
        return true;
    }

    /// <summary>Serializes the transaction and base64-encodes it - the form <c>sendTransaction</c> accepts.</summary>
    /// <returns>The base64-encoded transaction.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public string ToBase64() => Convert.ToBase64String(Serialize());

    private static PublicKey[] CopyRequiredSignerKeys(ITransactionMessage message)
    {
        var keys = new PublicKey[message.RequiredSignatures];
        for (var i = 0; i < keys.Length; i++)
            keys[i] = message.AccountKeys[i];

        return keys;
    }

    private static int RequiredSignerIndex(IReadOnlyList<PublicKey> requiredSignerKeys, PublicKey key)
    {
        for (var i = 0; i < requiredSignerKeys.Count; i++)
            if (requiredSignerKeys[i] == key)
                return i;

        return -1;
    }
}
