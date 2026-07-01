using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// A transaction: an <see cref="ITransactionMessage"/> (legacy <see cref="Message"/> or <see cref="MessageV0"/>)
/// plus one signature slot per required signer. Sign it with <see cref="Sign"/>, then serialize with
/// <see cref="Serialize"/> or <see cref="ToBase64"/> to submit it.
/// </summary>
public sealed class Transaction
{
    /// <summary>The length of an Ed25519 signature in bytes (64).</summary>
    public const int SignatureLength = 64;

    private readonly byte[][] _signatures;

    private Transaction(ITransactionMessage message)
    {
        Message = message;
        _signatures = new byte[message.RequiredSignatures][];
        for (var i = 0; i < _signatures.Length; i++)
            _signatures[i] = new byte[SignatureLength];
    }

    private Transaction(ITransactionMessage message, byte[][] signatures)
    {
        Message = message;
        _signatures = signatures;
    }

    /// <summary>The message being signed and sent.</summary>
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

    /// <summary>Parses a transaction from its wire bytes: the signatures followed by a legacy or v0 message.</summary>
    /// <param name="data">The serialized transaction.</param>
    /// <returns>The parsed transaction, carrying its signatures.</returns>
    /// <exception cref="FormatException">The data is truncated, a compact-u16 length in it is malformed, or the message is invalid.</exception>
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

            return new Transaction(message, signatures);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Span indexing and slicing throw index errors on short input; surface the documented type.
            throw new FormatException("The transaction data is truncated.", exception);
        }
    }

    /// <summary>
    /// Signs the message with each signer, placing each signature in the slot matching the signer's position
    /// among the required signers.
    /// </summary>
    /// <param name="signers">The signers to apply; each must be a required signer of the message.</param>
    /// <returns>This transaction, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A signer is not one of the message's required signers.</exception>
    public Transaction Sign(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var message = Message.Serialize();
        foreach (var signer in signers)
        {
            var index = RequiredSignerIndex(signer.PublicKey);
            if (index < 0)
                throw new ArgumentException($"{signer.PublicKey} is not a required signer of this transaction.", nameof(signers));

            _signatures[index] = signer.Sign(message);
        }

        return this;
    }

    /// <summary>Serializes the transaction to its wire bytes: the signatures followed by the message.</summary>
    /// <returns>The serialized transaction.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public byte[] Serialize()
    {
        using var buffer = new MemoryStream();
        buffer.Write(ShortVec.Encode(_signatures.Length));
        foreach (var signature in _signatures)
            buffer.Write(signature);

        buffer.Write(Message.Serialize());
        return buffer.ToArray();
    }

    /// <summary>Serializes the transaction and base64-encodes it - the form <c>sendTransaction</c> accepts.</summary>
    /// <returns>The base64-encoded transaction.</returns>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    public string ToBase64() => Convert.ToBase64String(Serialize());

    private int RequiredSignerIndex(PublicKey key)
    {
        for (var i = 0; i < Message.RequiredSignatures; i++)
            if (Message.AccountKeys[i] == key)
                return i;

        return -1;
    }
}
