using System.Security.Cryptography;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// A transaction: an <see cref="ITransactionMessage"/> plus one signature slot per required signer.
/// Legacy and v0 transactions encode signatures before the message; SIMD-0385 V1 transactions encode
/// the message first and signatures afterward. Sign it with <see cref="Sign"/>, then serialize with
/// <see cref="Serialize()"/> or <see cref="ToBase64"/> to submit it.
/// </summary>
public sealed class Transaction
{
    /// <summary>The length of an Ed25519 signature in bytes (64).</summary>
    public const int SignatureLength = 64;

    private readonly Signature[] _signatures;
    private byte[]? _signedMessageBytes;
    private PublicKey[]? _signedRequiredSignerKeys;

    private Transaction(ITransactionMessage message)
    {
        Message = message;
        _signatures = new Signature[message.RequiredSignatures];
        Signatures = Array.AsReadOnly(_signatures);
    }

    private Transaction(ITransactionMessage message, Signature[] signatures, byte[] signedMessageBytes)
    {
        Message = message;
        _signatures = signatures;
        Signatures = Array.AsReadOnly(_signatures);
        _signedMessageBytes = signedMessageBytes;
        _signedRequiredSignerKeys = CopyRequiredSignerKeys(message);
    }

    /// <summary>
    /// The message being signed and sent. After the transaction is successfully signed or deserialized,
    /// serialization continues to use the captured message bytes even if this object graph is later mutated.
    /// </summary>
    public ITransactionMessage Message { get; }

    /// <summary>
    /// The signature slots in required-signer order. An all-zero <see cref="Signature"/> is an absent
    /// signature in a partially signed transaction. The returned view is read-only and reflects later signing.
    /// </summary>
    public IReadOnlyList<Signature> Signatures { get; }

    /// <summary>
    /// The required signer keys in signature-slot order. A defensive copy is returned because compiled message
    /// collections may be mutable; after the first signature, this uses the captured signer mapping.
    /// </summary>
    public IReadOnlyList<PublicKey> RequiredSignerKeys
        => [.. _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message)];

    /// <summary><c>true</c> when every required signature slot is nonzero.</summary>
    /// <remarks>This checks presence. Use <see cref="VerifySignatures"/> to verify the signatures cryptographically.</remarks>
    public bool IsFullySigned
    {
        get
        {
            foreach (var signature in _signatures)
                if (signature == default)
                    return false;

            return true;
        }
    }

    /// <summary>The wire-format version selected by <see cref="Message"/>.</summary>
    public TransactionVersion Version
        => Message switch
        {
            MessageV1 => TransactionVersion.V1,
            MessageV0 => TransactionVersion.V0,
            _ => TransactionVersion.Legacy
        };

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
    /// Parses a transaction from its version-routed wire bytes, retaining the exact parsed message bytes
    /// for stable reserialization. Legacy and v0 carry a compact signature count and signatures before the
    /// message; V1 begins with <see cref="MessageV1.VersionPrefix"/> and carries its fixed number of signatures
    /// after the message.
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
            if (data.Length == 0)
                throw new FormatException("The transaction data is empty.");

            if (data[0] == MessageV1.VersionPrefix)
                return DeserializeV1(data);
            if ((data[0] & MessageV0.VersionPrefix) != 0)
                throw new FormatException($"Invalid transaction discriminator 0x{data[0]:X2}.");

            return DeserializeLegacyOrV0(data);
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
    /// <remarks>
    /// This compatibility method permits a subset of required signers. Prefer <see cref="PartialSign"/> when
    /// that intent should be explicit, or <see cref="SignAll"/> when completion is required.
    /// </remarks>
    public Transaction Sign(params ISigner[] signers) => PartialSign(signers);

    /// <summary>
    /// Signs the message with any supplied subset of its required signers. Existing signature slots are
    /// retained, enabling multi-stage, hardware-wallet, and air-gapped signing workflows.
    /// </summary>
    /// <param name="signers">The subset of required signers to apply.</param>
    /// <returns>This transaction, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="signers"/> or one of its elements is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A signer is not required by the message or returns something other than 64 bytes.
    /// </exception>
    public Transaction PartialSign(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var requiredSignerKeys = _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message);
        var pending = new (int Index, Signature Signature)[signers.Length];
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

            pending[i].Signature = new Signature(signature);
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

    /// <summary>
    /// Applies the supplied signers and requires every signature slot to be populated when the call returns.
    /// Use <see cref="PartialSign"/> when only a subset of signers is currently available.
    /// </summary>
    /// <param name="signers">Required signers to apply.</param>
    /// <returns>This fully populated transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> or an element is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A signer is not required or returns an invalid-length signature.</exception>
    /// <exception cref="InvalidOperationException">At least one required signature remains absent.</exception>
    public Transaction SignAll(params ISigner[] signers)
    {
        PartialSign(signers);
        if (!IsFullySigned)
            throw new InvalidOperationException("The transaction is not fully signed; at least one required signature is absent.");

        return this;
    }

    /// <summary>
    /// Adds a signature produced outside this process to its required signer slot after verifying it against
    /// the exact transaction message.
    /// </summary>
    /// <param name="signer">The required signer key that produced the signature.</param>
    /// <param name="signature">The externally produced signature.</param>
    /// <returns>This transaction, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="signer"/> is not a required signer.</exception>
    /// <exception cref="CryptographicException">The signature does not verify for the signer and message.</exception>
    public Transaction AddSignature(PublicKey signer, Signature signature)
    {
        var requiredSignerKeys = _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message);
        var index = RequiredSignerIndex(requiredSignerKeys, signer);
        if (index < 0)
            throw new ArgumentException($"{signer} is not a required signer of this transaction.", nameof(signer));

        var message = _signedMessageBytes ?? Message.Serialize();
        if (!signature.Verify(signer, message))
            throw new CryptographicException("The signature does not verify for this signer and transaction message.");

        _signatures[index] = signature;
        _signedMessageBytes ??= message;
        _signedRequiredSignerKeys ??= requiredSignerKeys;
        return this;
    }

    /// <summary>Returns the signature in <paramref name="signer"/>'s required slot.</summary>
    /// <param name="signer">A required signer key.</param>
    /// <returns>The signature, or the all-zero value when that signer has not signed yet.</returns>
    /// <exception cref="ArgumentException"><paramref name="signer"/> is not a required signer.</exception>
    public Signature GetSignature(PublicKey signer)
    {
        var requiredSignerKeys = _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message);
        var index = RequiredSignerIndex(requiredSignerKeys, signer);
        if (index < 0)
            throw new ArgumentException($"{signer} is not a required signer of this transaction.", nameof(signer));

        return _signatures[index];
    }

    /// <summary>Returns the exact message bytes that the current signature slots cover.</summary>
    /// <returns>A defensive copy of the captured or currently serialized message.</returns>
    public byte[] GetMessageBytes() => [.. _signedMessageBytes ?? Message.Serialize()];

    /// <summary>Computes the domain-separated Solana hash of <see cref="GetMessageBytes"/>.</summary>
    /// <returns>The 32-byte transaction message hash.</returns>
    public Hash GetMessageHash() => TransactionMessageHash.Compute(_signedMessageBytes ?? Message.Serialize());

    /// <summary>Verifies every required signature against the exact transaction message.</summary>
    /// <returns><c>true</c> only when every slot contains a valid signature.</returns>
    public bool VerifySignatures()
    {
        foreach (var result in VerifySignaturesWithResults())
            if (!result)
                return false;

        return true;
    }

    /// <summary>Verifies each signature independently, in required-signer order.</summary>
    /// <returns>One result per signature slot; absent all-zero signatures produce <c>false</c>.</returns>
    public IReadOnlyList<bool> VerifySignaturesWithResults()
    {
        var message = _signedMessageBytes ?? Message.Serialize();
        var requiredSignerKeys = _signedRequiredSignerKeys ?? CopyRequiredSignerKeys(Message);
        var results = new bool[_signatures.Length];
        for (var index = 0; index < results.Length; index++)
            results[index] = _signatures[index].Verify(requiredSignerKeys[index], message);

        return results;
    }

    /// <summary>Verifies every signature and returns the transaction message hash.</summary>
    /// <returns>The domain-separated message hash.</returns>
    /// <exception cref="CryptographicException">At least one required signature is absent or invalid.</exception>
    public Hash VerifyAndHashMessage()
    {
        if (!VerifySignatures())
            throw new CryptographicException("At least one transaction signature is absent or invalid.");

        return GetMessageHash();
    }

    /// <summary>
    /// Serializes the transaction to its version-specific wire bytes. Legacy and v0 place signatures first;
    /// V1 places the message first and its fixed number of signatures last.
    /// </summary>
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
    {
        var messageLength = _signedMessageBytes?.Length ?? Message.GetSerializedLength();
        var signaturesLength = _signatures.Length * SignatureLength;
        return Message is MessageV1
            ? messageLength + signaturesLength
            : ShortVec.GetByteCount(_signatures.Length) + signaturesLength + messageLength;
    }

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

        var offset = 0;
        if (Message is MessageV1)
        {
            offset += SerializeMessage(destination[offset..]);
            offset += SerializeSignatures(destination[offset..]);
        }
        else
        {
            offset += ShortVec.Encode(_signatures.Length, destination);
            offset += SerializeSignatures(destination[offset..]);
            offset += SerializeMessage(destination[offset..]);
        }

        written = offset;
        return true;
    }

    /// <summary>Serializes the transaction and base64-encodes it - the form <c>sendTransaction</c> accepts.</summary>
    /// <returns>The base64-encoded transaction.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public string ToBase64() => Convert.ToBase64String(Serialize());

    private static Transaction DeserializeLegacyOrV0(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var signatureCount = ShortVec.Decode(data, out var read);
        offset += read;
        var signatureBytes = (long)signatureCount * SignatureLength;
        if (signatureBytes >= data.Length - offset)
            throw new FormatException(
                $"The transaction declares {signatureCount} signature slot(s), but the remaining data cannot hold the signatures and a message.");

        var signatures = new Signature[signatureCount];
        for (var index = 0; index < signatureCount; index++)
        {
            signatures[index] = new Signature(data.Slice(offset, SignatureLength));
            offset += SignatureLength;
        }

        var messageBytes = data[offset..];
        ITransactionMessage message;
        if (messageBytes[0] == MessageV0.VersionPrefix)
        {
            message = MessageV0.Deserialize(messageBytes);
        }
        else if ((messageBytes[0] & MessageV0.VersionPrefix) != 0)
        {
            throw new FormatException($"Invalid message version byte 0x{messageBytes[0]:X2} in a legacy/v0 transaction envelope.");
        }
        else
        {
            message = global::SolSharp.Programs.Message.Deserialize(messageBytes);
        }

        // Solana's sanitize step requires exactly one signature slot per required signer (a partially
        // signed transaction carries zeroed slots, never fewer). Accepting a mismatch here would let
        // Sign index past the slot array and surface as an unrelated IndexOutOfRangeException.
        if (signatureCount != message.RequiredSignatures)
            throw new FormatException(
                $"The transaction carries {signatureCount} signature slot(s) but its message requires {message.RequiredSignatures}.");

        return new Transaction(message, signatures, messageBytes.ToArray());
    }

    private static Transaction DeserializeV1(ReadOnlySpan<byte> data)
    {
        var message = MessageV1.DeserializeTransactionMessage(data, out var messageLength);
        var signatureBytes = message.RequiredSignatures * SignatureLength;
        var remaining = data.Length - messageLength;
        if (remaining != signatureBytes)
            throw new FormatException(
                $"The V1 transaction message requires {message.RequiredSignatures} signature slot(s) ({signatureBytes} bytes), but {remaining} byte(s) remain.");

        var signatures = new Signature[message.RequiredSignatures];
        var offset = messageLength;
        for (var index = 0; index < signatures.Length; index++)
        {
            signatures[index] = new Signature(data.Slice(offset, SignatureLength));
            offset += SignatureLength;
        }

        return new Transaction(message, signatures, data[..messageLength].ToArray());
    }

    private int SerializeMessage(Span<byte> destination)
    {
        if (_signedMessageBytes is not { } signedMessage)
            return Message.Serialize(destination);

        signedMessage.CopyTo(destination);
        return signedMessage.Length;
    }

    private int SerializeSignatures(Span<byte> destination)
    {
        var offset = 0;
        foreach (var signature in _signatures)
        {
            signature.CopyTo(destination[offset..]);
            offset += SignatureLength;
        }

        return offset;
    }

    private static PublicKey[] CopyRequiredSignerKeys(ITransactionMessage message)
    {
        var keys = new PublicKey[message.RequiredSignatures];
        for (var i = 0; i < keys.Length; i++)
            keys[i] = message.AccountKeys[i];

        return keys;
    }

    private static int RequiredSignerIndex(PublicKey[] requiredSignerKeys, PublicKey key)
    {
        for (var i = 0; i < requiredSignerKeys.Length; i++)
            if (requiredSignerKeys[i] == key)
                return i;

        return -1;
    }
}
