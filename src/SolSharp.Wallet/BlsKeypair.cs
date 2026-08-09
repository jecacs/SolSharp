using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// An in-memory minimal-public-key-size BLS12-381 keypair compatible with the pinned Solana SDK.
/// The 32-byte little-endian secret is zeroed on disposal or by the finalizer backstop.
/// </summary>
/// <remarks>
/// The vetted native backend currently ships for linux-x64, linux-arm64, osx-x64, osx-arm64, and
/// win-x64. Other RIDs fail with <see cref="PlatformNotSupportedException"/> or a native-loader error.
/// </remarks>
public sealed class BlsKeypair : IDisposable
{
    /// <summary>The canonical little-endian secret-key length.</summary>
    public const int SecretKeyLength = BlsOperations.SecretKeyLength;

    /// <summary>The uncompressed G1 public-key length used by the Rust keypair file format.</summary>
    public const int UncompressedPublicKeyLength = 96;

    /// <summary>The exact keypair file length: 32-byte secret followed by the 96-byte uncompressed public key.</summary>
    public const int KeypairLength = SecretKeyLength + UncompressedPublicKeyLength;

    /// <summary>The minimum input-key-material length accepted by the pinned SDK's key derivation.</summary>
    public const int MinimumInputKeyMaterialLength = 32;

    /// <summary>The length of <c>ALPENGLOW || vote_account</c>.</summary>
    public const int VoteProofPayloadLength = 41;

    private static ReadOnlySpan<byte> VoteProofDomain => "ALPENGLOW"u8;

    private readonly object _secretGate = new();
    private readonly byte[] _secretKey;
    private bool _disposed;

    private BlsKeypair(byte[] secretKey)
    {
        _secretKey = secretKey;
        PublicKey = BlsPublicKey.FromValidated(BlsOperations.DerivePublicKey(secretKey));
        PopVerifiedPublicKey = new BlsPopVerifiedPublicKey(PublicKey);
    }

    /// <summary>The validated compressed BLS public key.</summary>
    public BlsPublicKey PublicKey { get; }

    /// <summary>
    /// The derived public key with trusted proof-of-possession provenance. Because this key was derived from
    /// the locally held secret, it matches the pinned Rust keypair's <c>PopVerified</c> public-key boundary.
    /// </summary>
    public BlsPopVerifiedPublicKey PopVerifiedPublicKey { get; }

    /// <summary>Generates a keypair by applying the pinned BLS key-generation function to 32 random bytes.</summary>
    /// <returns>A fresh BLS keypair.</returns>
    public static BlsKeypair Generate()
    {
        Span<byte> inputKeyMaterial = stackalloc byte[MinimumInputKeyMaterialLength];
        RandomNumberGenerator.Fill(inputKeyMaterial);
        try
        {
            return Derive(inputKeyMaterial);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputKeyMaterial);
        }
    }

    /// <summary>Derives a BLS keypair with <c>blst_keygen</c>, matching the pinned Rust SDK.</summary>
    /// <param name="inputKeyMaterial">At least 32 bytes of high-entropy input key material.</param>
    /// <returns>The deterministically derived keypair.</returns>
    /// <exception cref="ArgumentException">The input contains fewer than 32 bytes.</exception>
    public static BlsKeypair Derive(ReadOnlySpan<byte> inputKeyMaterial) =>
        Create(BlsOperations.DeriveSecretKey(inputKeyMaterial));

    /// <summary>
    /// Derives a BLS keypair from an Ed25519 signer by signing
    /// <c>bls-key-derive- || public_seed</c> and using the nonzero signature as input key material.
    /// </summary>
    /// <param name="signer">The Solana signer providing the derivation signature.</param>
    /// <param name="publicSeed">Application-visible bytes that distinguish derived BLS keys.</param>
    /// <returns>The deterministic BLS keypair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signer"/> is <see langword="null"/>.</exception>
    /// <exception cref="CryptographicException">The signer returns a malformed or all-zero placeholder signature.</exception>
    public static BlsKeypair DeriveFromSigner(ISigner signer, ReadOnlySpan<byte> publicSeed)
    {
        ArgumentNullException.ThrowIfNull(signer);

        var message = GC.AllocateUninitializedArray<byte>(checked(15 + publicSeed.Length));
        "bls-key-derive-"u8.CopyTo(message);
        publicSeed.CopyTo(message.AsSpan(15));

        byte[]? signature = null;
        try
        {
            signature = signer.Sign(message);
            if (signature.Length != Signature.Length)
                throw new CryptographicException($"The derivation signer returned {signature.Length} bytes instead of {Signature.Length}.");

            Span<byte> zero = stackalloc byte[Signature.Length];
            if (CryptographicOperations.FixedTimeEquals(signature, zero))
                throw new CryptographicException("An all-zero placeholder signature cannot be used as BLS input key material.");

            return Derive(signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
            if (signature is not null)
                CryptographicOperations.ZeroMemory(signature);
        }
    }

    /// <summary>Imports a canonical, nonzero 32-byte little-endian BLS secret scalar.</summary>
    /// <param name="secretKey">The canonical little-endian scalar.</param>
    /// <returns>The imported keypair.</returns>
    /// <exception cref="ArgumentException">The scalar has the wrong length, is noncanonical, or is zero.</exception>
    public static BlsKeypair FromSecretKey(ReadOnlySpan<byte> secretKey)
    {
        if (!BlsOperations.IsCanonicalSecretKey(secretKey))
            throw new ArgumentException("BLS secret key must be a canonical nonzero 32-byte little-endian scalar.", nameof(secretKey));

        return Create(secretKey.ToArray());
    }

    /// <summary>
    /// Imports the pinned Rust keypair representation: a canonical 32-byte little-endian secret
    /// followed by its exact 96-byte uncompressed G1 public key.
    /// </summary>
    /// <param name="keypair">The complete 128-byte keypair representation.</param>
    /// <returns>The validated keypair.</returns>
    /// <exception cref="ArgumentException">
    /// The value has the wrong length, contains an invalid secret, or its public half does not match the secret.
    /// </exception>
    public static BlsKeypair FromBytes(ReadOnlySpan<byte> keypair)
    {
        if (keypair.Length != KeypairLength)
            throw new ArgumentException($"BLS keypair must be exactly {KeypairLength} bytes.", nameof(keypair));

        var secretKey = keypair[..SecretKeyLength];
        if (!BlsOperations.IsCanonicalSecretKey(secretKey))
            throw new ArgumentException("BLS keypair contains a noncanonical or zero secret scalar.", nameof(keypair));

        var expectedPublicKey = BlsOperations.DeriveUncompressedPublicKey(secretKey);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(expectedPublicKey, keypair[SecretKeyLength..]))
                throw new ArgumentException("BLS keypair public key does not match its secret scalar.", nameof(keypair));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedPublicKey);
        }

        return Create(secretKey.ToArray());
    }

    /// <summary>
    /// Imports a JSON byte array in the 128-byte pinned Rust keypair-file representation.
    /// The immutable input string can contain secret material that cannot be cleared.
    /// </summary>
    /// <param name="json">A JSON array of exactly 128 integers in the range 0 through 255.</param>
    /// <returns>The validated keypair.</returns>
    /// <exception cref="ArgumentException">The input is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">The JSON or keypair representation is invalid.</exception>
    public static BlsKeypair FromJsonArray(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return FromJsonValues(JsonSerializer.Deserialize(json, WalletJsonContext.Default.Int32Array));
        }
        catch (JsonException exception)
        {
            throw new FormatException("BLS keypair is not a valid JSON number array.", exception);
        }
    }

    /// <summary>
    /// Imports a UTF-8 JSON byte array in the 128-byte pinned Rust keypair-file representation.
    /// The caller retains ownership of the input buffer and should clear it after use.
    /// </summary>
    /// <param name="utf8Json">UTF-8 JSON containing exactly 128 integers in the range 0 through 255.</param>
    /// <returns>The validated keypair.</returns>
    /// <exception cref="ArgumentException">The input is empty.</exception>
    /// <exception cref="FormatException">The JSON or keypair representation is invalid.</exception>
    public static BlsKeypair FromJsonArray(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
            throw new ArgumentException("BLS keypair JSON cannot be empty.", nameof(utf8Json));

        try
        {
            return FromJsonValues(JsonSerializer.Deserialize(utf8Json, WalletJsonContext.Default.Int32Array));
        }
        catch (JsonException exception)
        {
            throw new FormatException("BLS keypair is not a valid UTF-8 JSON number array.", exception);
        }
    }

    private static BlsKeypair FromJsonValues(int[]? values)
    {
        if (values is null)
            throw new FormatException($"BLS keypair JSON must contain exactly {KeypairLength} byte values.");

        byte[]? bytes = null;
        try
        {
            if (values.Length != KeypairLength)
                throw new FormatException($"BLS keypair JSON must contain exactly {KeypairLength} byte values.");

            bytes = new byte[KeypairLength];
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is < byte.MinValue or > byte.MaxValue)
                    throw new FormatException($"BLS keypair JSON value at index {i} is outside the byte range.");

                bytes[i] = (byte)values[i];
            }

            try
            {
                return FromBytes(bytes);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("BLS keypair JSON contains an invalid keypair.", exception);
            }
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
            Array.Clear(values);
        }
    }

    /// <summary>Signs a message with the exact pinned POP-ciphersuite signature DST.</summary>
    /// <param name="message">The exact bytes to sign.</param>
    /// <returns>The validated compressed G2 signature.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public BlsSignature Sign(ReadOnlySpan<byte> message)
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return BlsSignature.FromValidated(BlsOperations.Sign(_secretKey, message));
        }
    }

    /// <summary>Verifies a signature through this keypair's proof-of-possession-verified public key.</summary>
    /// <param name="signature">The validated compressed G2 signature.</param>
    /// <param name="message">The exact signed message.</param>
    /// <returns><see langword="true"/> only for a valid signature by this keypair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <see langword="null"/>.</exception>
    public bool Verify(BlsSignature signature, ReadOnlySpan<byte> message)
        => PopVerifiedPublicKey.Verify(signature, message);

    /// <summary>Creates a proof bound to <c>payload || compressed_public_key</c> with the pinned POP DST.</summary>
    /// <param name="payload">The application payload to bind.</param>
    /// <returns>The validated compressed G2 proof.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public BlsProofOfPossession CreateProofOfPossession(ReadOnlySpan<byte> payload)
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return BlsProofOfPossession.FromValidated(
                BlsOperations.CreateProofOfPossession(_secretKey, PublicKey.Bytes, payload));
        }
    }

    /// <summary>Creates the vote-program proof bound to <c>ALPENGLOW || vote_account</c>.</summary>
    /// <param name="voteAccount">The vote account that will store the BLS public key.</param>
    /// <returns>The validated compressed G2 proof.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public BlsProofOfPossession CreateVoteProofOfPossession(PublicKey voteAccount)
    {
        Span<byte> payload = stackalloc byte[VoteProofPayloadLength];
        WriteVoteProofPayload(voteAccount, payload);
        return CreateProofOfPossession(payload);
    }

    /// <summary>
    /// Exports a copy of the canonical 32-byte little-endian secret. The caller owns the returned
    /// secret buffer and should clear it with <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>.
    /// </summary>
    /// <returns>A new secret-key buffer.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public byte[] ToSecretKeyBytes()
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return [.. _secretKey];
        }
    }

    /// <summary>
    /// Exports the exact 128-byte Rust keypair representation: the 32-byte little-endian secret
    /// followed by the derived 96-byte uncompressed public key. The caller must clear the result.
    /// </summary>
    /// <returns>A new secret-bearing keypair buffer.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public byte[] ToBytes()
    {
        lock (_secretGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var bytes = new byte[KeypairLength];
            byte[]? publicKey = null;
            try
            {
                _secretKey.CopyTo(bytes, 0);
                publicKey = BlsOperations.DeriveUncompressedPublicKey(_secretKey);
                publicKey.CopyTo(bytes, SecretKeyLength);
                return bytes;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
            finally
            {
                if (publicKey is not null)
                    CryptographicOperations.ZeroMemory(publicKey);
            }
        }
    }

    /// <summary>
    /// Exports the 128-byte keypair as the JSON number array used by the pinned Rust SDK. The
    /// returned immutable string contains secret material and cannot be zeroed; prefer <see cref="ToBytes"/>.
    /// </summary>
    /// <returns>A JSON array containing the secret and uncompressed public key.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public string ToJsonArray()
    {
        var utf8Json = ToJsonUtf8Bytes();
        try
        {
            return Encoding.UTF8.GetString(utf8Json);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(utf8Json);
        }
    }

    /// <summary>
    /// Exports the 128-byte keypair as a zeroable UTF-8 JSON number array compatible with the
    /// pinned Rust SDK. The caller owns the returned secret-bearing buffer and must clear it.
    /// </summary>
    /// <returns>A UTF-8 JSON buffer containing the secret and uncompressed public key.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has been disposed.</exception>
    public byte[] ToJsonUtf8Bytes()
    {
        var bytes = ToBytes();
        int[]? values = null;
        try
        {
            values = new int[KeypairLength];
            for (var i = 0; i < bytes.Length; i++)
                values[i] = bytes[i];

            return JsonSerializer.SerializeToUtf8Bytes(values, WalletJsonContext.Default.Int32Array);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (values is not null)
                Array.Clear(values);
        }
    }

    /// <summary>Zeroes the in-memory BLS secret. Secret-dependent operations throw afterwards.</summary>
    public void Dispose()
    {
        ClearSecret();
        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer backstop that clears the managed secret when deterministic disposal was missed.</summary>
    ~BlsKeypair()
    {
        ClearSecret();
    }

    internal static void WriteVoteProofPayload(PublicKey voteAccount, Span<byte> destination)
    {
        if (destination.Length != VoteProofPayloadLength)
            throw new ArgumentException($"Vote proof payload must be {VoteProofPayloadLength} bytes.", nameof(destination));

        VoteProofDomain.CopyTo(destination);
        voteAccount.CopyTo(destination[VoteProofDomain.Length..]);
    }

    private static BlsKeypair Create(byte[] ownedSecretKey)
    {
        try
        {
            return new BlsKeypair(ownedSecretKey);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(ownedSecretKey);
            throw;
        }
    }

    private void ClearSecret()
    {
        lock (_secretGate)
        {
            if (_disposed)
                return;

            CryptographicOperations.ZeroMemory(_secretKey);
            _disposed = true;
        }
    }
}
