using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>The payload encoding selected for a version-0 Solana off-chain message.</summary>
public enum OffchainMessageFormat : byte
{
    /// <summary>Printable ASCII bytes in the ledger-sized payload range.</summary>
    RestrictedAscii = 0,

    /// <summary>Valid UTF-8 bytes in the ledger-sized payload range.</summary>
    LimitedUtf8 = 1,

    /// <summary>Valid UTF-8 bytes above the ledger-sized range.</summary>
    ExtendedUtf8 = 2
}

/// <summary>
/// A version-0 Solana off-chain message. Its domain-separated bytes can be signed by a Solana key
/// without constructing a transaction or granting any on-chain authority.
/// </summary>
public sealed class OffchainMessage : IEquatable<OffchainMessage>
{
    /// <summary>The only version supported by the pinned Rust contract.</summary>
    public const byte CurrentVersion = 0;

    /// <summary>The signing-domain plus version header length.</summary>
    public const int HeaderLength = 17;

    /// <summary>The largest version-0 payload representable by the wire format.</summary>
    public const int MaxMessageLength = 65_515;

    /// <summary>The largest version-0 payload accepted in a Solana ledger packet.</summary>
    public const int MaxLedgerMessageLength = 1_212;

    private const int VersionHeaderLength = 3;

    private static readonly byte[] SigningDomain =
    [
        0xFF,
        0x73,
        0x6F,
        0x6C,
        0x61,
        0x6E,
        0x61,
        0x20,
        0x6F,
        0x66,
        0x66,
        0x63,
        0x68,
        0x61,
        0x69,
        0x6E
    ];

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly byte[] _message;

    private OffchainMessage(OffchainMessageFormat format, ReadOnlySpan<byte> message)
    {
        Format = format;
        _message = message.ToArray();
    }

    /// <summary>The off-chain message version.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The wire version belongs to each parsed message instance.")]
    public byte Version => CurrentVersion;

    /// <summary>The payload format encoded in the wire header.</summary>
    public OffchainMessageFormat Format { get; }

    /// <summary>The payload length in bytes.</summary>
    public int MessageLength => _message.Length;

    /// <summary>Creates a version-0 off-chain message and selects its canonical payload format.</summary>
    /// <param name="message">A non-empty printable-ASCII or valid-UTF-8 payload.</param>
    /// <returns>The validated off-chain message.</returns>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="message"/> exceeds <see cref="MaxMessageLength"/> bytes.</exception>
    public static OffchainMessage Create(ReadOnlySpan<byte> message) => Create(CurrentVersion, message);

    /// <summary>Creates an off-chain message for an explicitly selected wire version.</summary>
    /// <param name="version">The wire version; only <see cref="CurrentVersion"/> is currently defined.</param>
    /// <param name="message">A non-empty printable-ASCII or valid-UTF-8 payload.</param>
    /// <returns>The validated off-chain message.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="version"/> is unsupported or <paramref name="message"/> exceeds
    /// <see cref="MaxMessageLength"/> bytes.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty or is not valid UTF-8.</exception>
    public static OffchainMessage Create(byte version, ReadOnlySpan<byte> message)
    {
        if (version != CurrentVersion)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Only off-chain message version 0 is supported.");
        if (message.IsEmpty)
            throw new ArgumentException("An off-chain message cannot be empty.", nameof(message));
        if (message.Length > MaxMessageLength)
            throw new ArgumentOutOfRangeException(nameof(message), message.Length, $"An off-chain message cannot exceed {MaxMessageLength} bytes.");

        OffchainMessageFormat format;
        if (message.Length <= MaxLedgerMessageLength && IsPrintableAscii(message))
            format = OffchainMessageFormat.RestrictedAscii;
        else if (message.Length <= MaxLedgerMessageLength && IsUtf8(message))
            format = OffchainMessageFormat.LimitedUtf8;
        else if (IsUtf8(message))
            format = OffchainMessageFormat.ExtendedUtf8;
        else
            throw new ArgumentException("An off-chain message must contain valid UTF-8.", nameof(message));

        return new OffchainMessage(format, message);
    }

    /// <summary>Creates a version-0 off-chain message from valid .NET text.</summary>
    /// <param name="message">A non-empty string whose strict UTF-8 representation is within the wire limit.</param>
    /// <returns>The validated off-chain message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty or contains invalid Unicode.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The UTF-8 representation exceeds <see cref="MaxMessageLength"/> bytes.</exception>
    public static OffchainMessage Create(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            return Create(StrictUtf8.GetBytes(message));
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("An off-chain message must contain valid Unicode text.", nameof(message), exception);
        }
    }

    /// <summary>Parses and validates a complete domain-separated off-chain message.</summary>
    /// <param name="data">The complete serialized bytes.</param>
    /// <returns>The decoded version-0 message.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="data"/> has a wrong domain, version, length, format, or payload encoding.
    /// </exception>
    public static OffchainMessage Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length is < HeaderLength + VersionHeaderLength + 1 or > ushort.MaxValue)
            throw new FormatException("Off-chain message length is outside the version-0 wire range.");
        if (!data[..SigningDomain.Length].SequenceEqual(SigningDomain))
            throw new FormatException("Off-chain message signing domain is invalid.");
        if (data[SigningDomain.Length] != CurrentVersion)
            throw new FormatException($"Unsupported off-chain message version {data[SigningDomain.Length]}.");

        var formatByte = data[HeaderLength];
        if (formatByte > (byte)OffchainMessageFormat.ExtendedUtf8)
            throw new FormatException($"Unsupported off-chain message format {formatByte}.");

        var messageLength = data[HeaderLength + 1] | (data[HeaderLength + 2] << 8);
        if (messageLength == 0 || HeaderLength + VersionHeaderLength + messageLength != data.Length)
            throw new FormatException("Off-chain message payload length does not match its header.");

        var format = (OffchainMessageFormat)formatByte;
        var message = data[(HeaderLength + VersionHeaderLength)..];
        var valid = format switch
        {
            OffchainMessageFormat.RestrictedAscii => message.Length <= MaxLedgerMessageLength && IsPrintableAscii(message),
            OffchainMessageFormat.LimitedUtf8 => message.Length <= MaxLedgerMessageLength && IsUtf8(message),
            OffchainMessageFormat.ExtendedUtf8 => message.Length <= MaxMessageLength && IsUtf8(message),
            _ => false
        };
        if (!valid)
            throw new FormatException("Off-chain message payload does not satisfy its declared format.");

        return new OffchainMessage(format, message);
    }

    /// <summary>Returns a defensive copy of the payload bytes, without the wire header.</summary>
    /// <returns>A new byte array containing the message payload.</returns>
    public byte[] ToMessageBytes() => [.. _message];

    /// <summary>Returns the exact domain-separated bytes that are hashed and signed.</summary>
    /// <returns>The complete serialized message.</returns>
    public byte[] Serialize()
    {
        var bytes = new byte[HeaderLength + VersionHeaderLength + _message.Length];
        SigningDomain.CopyTo(bytes, 0);
        bytes[SigningDomain.Length] = CurrentVersion;
        bytes[HeaderLength] = (byte)Format;
        bytes[HeaderLength + 1] = (byte)_message.Length;
        bytes[HeaderLength + 2] = (byte)(_message.Length >> 8);
        _message.CopyTo(bytes, HeaderLength + VersionHeaderLength);
        return bytes;
    }

    /// <summary>Computes the SHA-256 hash of the exact serialized message.</summary>
    /// <returns>The typed Solana hash.</returns>
    public Hash ComputeHash() => new(SHA256.HashData(Serialize()));

    /// <summary>Signs the exact serialized message with a Solana signer.</summary>
    /// <param name="signer">The signer whose key will authenticate the message.</param>
    /// <returns>The typed Ed25519 signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signer"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The signer returns a value other than 64 bytes.</exception>
    public Signature Sign(ISigner signer)
    {
        ArgumentNullException.ThrowIfNull(signer);
        return new Signature(signer.Sign(Serialize()));
    }

    /// <summary>Verifies a signature over the exact serialized message.</summary>
    /// <param name="signer">The public key expected to have signed the message.</param>
    /// <param name="signature">The signature to verify.</param>
    /// <returns><c>true</c> if the signature is valid under Solana's strict Ed25519 rules.</returns>
    public bool Verify(PublicKey signer, Signature signature) => signature.Verify(signer, Serialize());

    /// <summary>Determines whether this message equals <paramref name="other"/>.</summary>
    /// <param name="other">The message to compare with.</param>
    /// <returns><c>true</c> if version, format, and payload bytes are equal.</returns>
    public bool Equals(OffchainMessage? other)
        => other is not null && Format == other.Format && _message.AsSpan().SequenceEqual(other._message);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is OffchainMessage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Format);
        foreach (var value in _message)
            hash.Add(value);
        return hash.ToHashCode();
    }

    private static bool IsPrintableAscii(ReadOnlySpan<byte> message)
    {
        foreach (var value in message)
        {
            if (value is < 0x20 or > 0x7E)
                return false;
        }

        return true;
    }

    private static bool IsUtf8(ReadOnlySpan<byte> message)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(message);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
