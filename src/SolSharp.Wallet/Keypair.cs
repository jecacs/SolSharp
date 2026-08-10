using System.Security.Cryptography;
using Org.BouncyCastle.Math.EC.Rfc8032;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// An in-memory Ed25519 keypair - the local <see cref="ISigner"/>, backed by a 32-byte secret seed.
/// The secret is held in managed memory and zeroed on <see cref="Dispose"/> - or, if disposal is
/// missed, by the finalizer when the keypair is garbage-collected.
/// </summary>
public sealed partial class Keypair : ISigner, IDisposable
{
    /// <summary>Length in bytes of an Ed25519 seed (the private half of the keypair).</summary>
    public const int SeedLength = 32;

    /// <summary>Length in bytes of a Solana secret key: the 32-byte seed followed by the 32-byte public key.</summary>
    public const int SecretKeyLength = 64;

    private readonly Lock _secretGate = new();
    private readonly byte[] _seed;
    private bool _disposed;

    private Keypair(byte[] seed)
    {
        _seed = seed;

        Span<byte> publicKey = stackalloc byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePublicKey(seed, publicKey);
        PublicKey = new(publicKey);
    }

    /// <summary>
    /// Backstop for a caller that forgets to dispose: the seed is still zeroed when the keypair is
    /// finalized. Deterministic <see cref="Dispose"/> is preferred (and suppresses this).
    /// </summary>
    ~Keypair()
    {
        ClearSecret();
    }

    /// <inheritdoc/>
    public PublicKey PublicKey { get; }

    /// <summary>Creates a new keypair from cryptographically secure random bytes.</summary>
    /// <returns>A fresh keypair.</returns>
    public static Keypair Generate()
        => new(RandomNumberGenerator.GetBytes(SeedLength));

    /// <summary>Creates a keypair from a 32-byte Ed25519 seed.</summary>
    /// <param name="seed">The 32-byte seed (the private half of the keypair).</param>
    /// <returns>The keypair whose public key is derived from <paramref name="seed"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="seed"/> is not <see cref="SeedLength"/> bytes long.</exception>
    public static Keypair FromSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedLength)
            throw new ArgumentException($"Seed must be {SeedLength} bytes, got {seed.Length}.", nameof(seed));

        return new([.. seed]);
    }

    /// <summary>
    /// Creates a keypair from a 64-byte Solana secret key (32-byte seed followed by the 32-byte public key).
    /// </summary>
    /// <param name="secretKey">The 64-byte secret key, as produced by <c>solana-keygen</c> and wallet exports.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="secretKey"/> is not <see cref="SecretKeyLength"/> bytes long, or its trailing public key
    /// does not match the one derived from the seed.
    /// </exception>
    public static Keypair FromSecretKey(ReadOnlySpan<byte> secretKey)
    {
        if (secretKey.Length != SecretKeyLength)
            throw new ArgumentException($"Secret key must be {SecretKeyLength} bytes, got {secretKey.Length}.", nameof(secretKey));

        var keypair = FromSeed(secretKey[..SeedLength]);

        Span<byte> derived = stackalloc byte[PublicKey.Length];
        keypair.PublicKey.CopyTo(derived);
        if (!CryptographicOperations.FixedTimeEquals(secretKey[SeedLength..], derived))
        {
            keypair.Dispose();
            throw new ArgumentException("Secret key's public-key half does not match its seed.", nameof(secretKey));
        }

        return keypair;
    }

    /// <summary>Signs <paramref name="message"/> with this keypair's private key.</summary>
    /// <param name="message">The bytes to sign; for a transaction, the serialized message.</param>
    /// <returns>The 64-byte Ed25519 signature.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public byte[] Sign(ReadOnlySpan<byte> message)
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // The span overload signs without copying the message - the only allocation is the signature.
            var signature = new byte[Ed25519.SignatureSize];
            Ed25519.Sign(_seed, message, signature);
            return signature;
        }
    }

    /// <summary>Signs <paramref name="message"/> and returns the signature as a typed Solana value.</summary>
    /// <param name="message">The bytes to sign; for a transaction, the serialized message.</param>
    /// <returns>The strict Ed25519 signature.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public Signature SignSignature(ReadOnlySpan<byte> message)
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Span<byte> signature = stackalloc byte[Signature.Length];
            Ed25519.Sign(_seed, message, signature);
            return new(signature);
        }
    }

    /// <summary>
    /// Exports the Solana 64-byte secret-key representation: the 32-byte Ed25519 seed followed by
    /// the derived 32-byte public key. The returned array contains secret material; the caller owns
    /// it and should clear it with <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> as
    /// soon as it is no longer needed.
    /// </summary>
    /// <returns>A new 64-byte secret-key array.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public byte[] ToBytes()
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var bytes = new byte[SecretKeyLength];
            _seed.CopyTo(bytes, 0);
            PublicKey.CopyTo(bytes.AsSpan(SeedLength));
            return bytes;
        }
    }

    /// <summary>
    /// Exports a copy of the 32-byte Ed25519 seed. The returned array contains secret material; the
    /// caller owns it and should clear it with <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>
    /// as soon as it is no longer needed.
    /// </summary>
    /// <returns>A new 32-byte seed array.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public byte[] ToSeedBytes()
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return [.. _seed];
        }
    }

    /// <summary>
    /// Exports the Solana 64-byte secret key as base58, matching wallet exports and the Rust SDK's
    /// <c>to_base58_string</c>. The returned immutable string contains secret material and cannot be
    /// zeroed; prefer <see cref="ToBytes"/> when the receiving API accepts bytes.
    /// </summary>
    /// <returns>The base58-encoded 64-byte secret key.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public string ToBase58String()
    {
        var bytes = ToBytes();
        try
        {
            return Base58.Encode(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Zeroes the in-memory secret seed. Signing after disposal throws.</summary>
    public void Dispose()
    {
        ClearSecret();
        GC.SuppressFinalize(this);
    }

    private void ClearSecret()
    {
        lock (_secretGate)
        {
            if (_disposed)
                return;

            CryptographicOperations.ZeroMemory(_seed);
            _disposed = true;
        }
    }
}
