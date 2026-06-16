using System.Security.Cryptography;
using Org.BouncyCastle.Math.EC.Rfc8032;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// An in-memory Ed25519 keypair - the local <see cref="ISigner"/>, backed by a 32-byte secret seed.
/// The secret is held in managed memory and zeroed on <see cref="Dispose"/>.
/// </summary>
public sealed partial class Keypair : ISigner, IDisposable
{
    /// <summary>Length in bytes of an Ed25519 seed (the private half of the keypair).</summary>
    public const int SeedLength = 32;

    /// <summary>Length in bytes of a Solana secret key: the 32-byte seed followed by the 32-byte public key.</summary>
    public const int SecretKeyLength = 64;

    private readonly byte[] _seed;
    private bool _disposed;

    private Keypair(byte[] seed)
    {
        _seed = seed;

        var publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePublicKey(seed, 0, publicKey, 0);
        PublicKey = new PublicKey(publicKey);
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

        return new Keypair(seed.ToArray());
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
        if (!secretKey[SeedLength..].SequenceEqual(derived))
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        var signature = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(_seed, 0, message.ToArray(), 0, message.Length, signature, 0);
        return signature;
    }

    /// <summary>Zeroes the in-memory secret seed. Signing after disposal throws.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        CryptographicOperations.ZeroMemory(_seed);
        _disposed = true;
    }
}
