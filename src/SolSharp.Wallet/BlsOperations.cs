using System.Security.Cryptography;
using Backend = Nethermind.Crypto.Bls;

namespace SolSharp.Wallet;

internal static class BlsOperations
{
    internal const int SecretKeyLength = 32;
    internal const int PublicKeyLength = 48;
    internal const int SignatureLength = 96;

    private static ReadOnlySpan<byte> SignatureDomain =>
        "BLS_SIG_BLS12381G2_XMD:SHA-256_SSWU_RO_POP_"u8;

    private static ReadOnlySpan<byte> ProofOfPossessionDomain =>
        "BLS_POP_BLS12381G2_XMD:SHA-256_SSWU_RO_POP_"u8;

    internal static bool TryDecodeCanonicalBase64(ReadOnlySpan<char> base64, Span<byte> destination)
    {
        if (base64.Length != checked(destination.Length / 3 * 4))
            return false;

        foreach (var value in base64)
        {
            if (!IsBase64Alphabet(value))
                return false;
        }

        return Convert.TryFromBase64Chars(base64, destination, out var bytesWritten) &&
               bytesWritten == destination.Length;
    }

    internal static byte[] DeriveSecretKey(ReadOnlySpan<byte> inputKeyMaterial)
    {
        if (inputKeyMaterial.Length < SecretKeyLength)
        {
            throw new ArgumentException(
                $"BLS input key material must contain at least {SecretKeyLength} bytes.",
                nameof(inputKeyMaterial));
        }

        var result = new byte[SecretKeyLength];
        try
        {
            var secretKey = new Backend.SecretKey(result);
            secretKey.Keygen(inputKeyMaterial);
            return result;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(result);
            throw;
        }
    }

    internal static bool IsCanonicalSecretKey(ReadOnlySpan<byte> secretKey)
    {
        if (secretKey.Length != SecretKeyLength)
            return false;

        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var backendKey = new Backend.SecretKey(decoded);
            backendKey.FromLendian(secretKey);
            return true;
        }
        catch (Backend.BlsException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    internal static byte[] DerivePublicKey(ReadOnlySpan<byte> secretKey)
    {
        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var backendKey = new Backend.SecretKey(decoded);
            backendKey.FromLendian(secretKey);
            var publicKey = new Backend.P1(backendKey);
            return publicKey.Compress();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    internal static byte[] DeriveUncompressedPublicKey(ReadOnlySpan<byte> secretKey)
    {
        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var backendKey = new Backend.SecretKey(decoded);
            backendKey.FromLendian(secretKey);
            var publicKey = new Backend.P1(backendKey);
            return publicKey.Serialize();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    internal static byte[] Sign(ReadOnlySpan<byte> secretKey, ReadOnlySpan<byte> message)
    {
        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var backendKey = new Backend.SecretKey(decoded);
            backendKey.FromLendian(secretKey);
            var signature = new Backend.P2().HashTo(message, SignatureDomain).SignWith(backendKey);
            return signature.Compress();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    internal static byte[] CreateProofOfPossession(
        ReadOnlySpan<byte> secretKey,
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> payload)
    {
        var boundPayload = GC.AllocateUninitializedArray<byte>(checked(payload.Length + PublicKeyLength));
        payload.CopyTo(boundPayload);
        publicKey.CopyTo(boundPayload.AsSpan(payload.Length));

        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var backendKey = new Backend.SecretKey(decoded);
            backendKey.FromLendian(secretKey);
            var proof = new Backend.P2().HashTo(boundPayload, ProofOfPossessionDomain).SignWith(backendKey);
            return proof.Compress();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
            CryptographicOperations.ZeroMemory(boundPayload);
        }
    }

    internal static bool IsValidPublicKey(ReadOnlySpan<byte> compressed) =>
        IsValidG1Point(compressed);

    internal static bool IsValidSignature(ReadOnlySpan<byte> compressed) =>
        GetG2ValidationResult(compressed) == BlsPointValidationResult.Valid;

    internal static byte[] AggregatePublicKeys(IReadOnlyList<BlsPopVerifiedPublicKey> publicKeys)
    {
        Span<long> point = stackalloc long[Backend.P1Affine.Sz];
        try
        {
            var decoded = new Backend.P1Affine(point);
            DecodePublicKey(publicKeys[0].Bytes, decoded);
            var aggregate = new Backend.P1(decoded);
            for (var i = 1; i < publicKeys.Count; i++)
            {
                DecodePublicKey(publicKeys[i].Bytes, decoded);
                aggregate.Add(decoded);
            }

            return aggregate.Compress();
        }
        catch (Backend.BlsException exception)
        {
            throw new CryptographicException("The native BLS backend rejected a validated public key during aggregation.", exception);
        }
    }

    internal static byte[] AggregateSignatures(IReadOnlyList<BlsSignature> signatures)
    {
        Span<long> point = stackalloc long[Backend.P2Affine.Sz];
        try
        {
            var decoded = new Backend.P2Affine(point);
            DecodeSignature(signatures[0].Bytes, decoded);
            var aggregate = new Backend.P2(decoded);
            for (var i = 1; i < signatures.Count; i++)
            {
                DecodeSignature(signatures[i].Bytes, decoded);
                aggregate.Add(decoded);
            }

            return aggregate.Compress();
        }
        catch (Backend.BlsException exception)
        {
            throw new CryptographicException("The native BLS backend rejected a validated signature during aggregation.", exception);
        }
    }

    internal static BlsPointValidationResult GetG2ValidationResult(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length != SignatureLength)
            return BlsPointValidationResult.BadEncoding;

        Span<long> point = stackalloc long[Backend.P2Affine.Sz];
        try
        {
            var decoded = new Backend.P2Affine(point);
            if (!decoded.TryDecode(compressed, out _))
                return BlsPointValidationResult.BadEncoding;
            if (!decoded.OnCurve())
                return BlsPointValidationResult.NotOnCurve;
            if (!decoded.InGroup())
                return BlsPointValidationResult.NotInGroup;
            return decoded.IsInf()
                ? BlsPointValidationResult.Infinity
                : BlsPointValidationResult.Valid;
        }
        catch (Backend.BlsException)
        {
            return BlsPointValidationResult.BadEncoding;
        }
    }

    internal static bool VerifySignature(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> message) =>
        Verify(publicKey, signature, message, SignatureDomain);

    internal static bool VerifyProofOfPossession(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> proof,
        ReadOnlySpan<byte> payload)
    {
        var boundPayload = GC.AllocateUninitializedArray<byte>(checked(payload.Length + PublicKeyLength));
        payload.CopyTo(boundPayload);
        publicKey.CopyTo(boundPayload.AsSpan(payload.Length));
        try
        {
            return Verify(publicKey, proof, boundPayload, ProofOfPossessionDomain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(boundPayload);
        }
    }

    private static bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> domain)
    {
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
            return false;

        Span<long> publicKeyPoint = stackalloc long[Backend.P1Affine.Sz];
        Span<long> signaturePoint = stackalloc long[Backend.P2Affine.Sz];
        try
        {
            var decodedPublicKey = new Backend.P1Affine(publicKeyPoint);
            var decodedSignature = new Backend.P2Affine(signaturePoint);
            if (!decodedPublicKey.TryDecode(publicKey, out _) ||
                !decodedPublicKey.OnCurve() ||
                !decodedPublicKey.InGroup() ||
                decodedPublicKey.IsInf() ||
                !decodedSignature.TryDecode(signature, out _) ||
                !decodedSignature.OnCurve() ||
                !decodedSignature.InGroup() ||
                decodedSignature.IsInf())
            {
                return false;
            }

            var hash = new Backend.P2().HashTo(message, domain);
            var left = new Backend.PT(decodedSignature, Backend.P1Affine.Generator());
            var right = new Backend.PT(hash.ToAffine(), decodedPublicKey);
            return Backend.PT.FinalVerify(left, right);
        }
        catch (Backend.BlsException)
        {
            return false;
        }
    }

    private static bool IsValidG1Point(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length != PublicKeyLength)
            return false;

        Span<long> point = stackalloc long[Backend.P1Affine.Sz];
        try
        {
            var decoded = new Backend.P1Affine(point);
            return decoded.TryDecode(compressed, out _) &&
                   decoded.OnCurve() &&
                   decoded.InGroup() &&
                   !decoded.IsInf();
        }
        catch (Backend.BlsException)
        {
            return false;
        }
    }

    private static void DecodePublicKey(ReadOnlySpan<byte> compressed, Backend.P1Affine decoded)
    {
        if (!decoded.TryDecode(compressed, out _) ||
            !decoded.OnCurve() ||
            !decoded.InGroup() ||
            decoded.IsInf())
        {
            throw new CryptographicException("A validated BLS public key no longer satisfies its point invariant.");
        }
    }

    private static void DecodeSignature(ReadOnlySpan<byte> compressed, Backend.P2Affine decoded)
    {
        if (!decoded.TryDecode(compressed, out _) ||
            !decoded.OnCurve() ||
            !decoded.InGroup() ||
            decoded.IsInf())
        {
            throw new CryptographicException("A validated BLS signature no longer satisfies its point invariant.");
        }
    }

    private static bool IsBase64Alphabet(char value) =>
        value is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or
            '+' or '/';
}

internal enum BlsPointValidationResult
{
    /// <summary>The point is valid.</summary>
    Valid,

    /// <summary>The byte representation cannot be decoded.</summary>
    BadEncoding,

    /// <summary>The decoded point is not on the curve.</summary>
    NotOnCurve,

    /// <summary>The decoded point is outside the prime-order subgroup.</summary>
    NotInGroup,

    /// <summary>The decoded point is the point at infinity.</summary>
    Infinity
}
