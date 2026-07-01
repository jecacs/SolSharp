using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SolSharp.Wallet;

/// <summary>
/// SLIP-0010 hierarchical key derivation for Ed25519 - the scheme Solana wallets (Phantom, Solflare) use
/// to derive accounts from a BIP-39 seed, conventionally at <c>m/44'/501'/account'/0'</c>. Ed25519 supports
/// hardened derivation only, so every path segment must carry a <c>'</c>.
/// See <see href="https://github.com/satoshilabs/slips/blob/master/slip-0010.md">SLIP-0010</see>.
/// </summary>
public static class Slip10
{
    private const uint HardenedOffset = 0x8000_0000;

    private static ReadOnlySpan<byte> MasterKey => "ed25519 seed"u8;

    /// <summary>Derives the 32-byte Ed25519 seed at <paramref name="path"/> from a BIP-39 <paramref name="seed"/>.</summary>
    /// <param name="seed">The BIP-39 seed (64 bytes from <see cref="Bip39.ToSeed"/>; any non-empty length is accepted).</param>
    /// <param name="path">The derivation path, e.g. <c>m/44'/501'/0'/0'</c>; every segment must be hardened.</param>
    /// <returns>The derived 32-byte Ed25519 seed, usable with <see cref="Keypair.FromSeed"/>. It is key material: zero it after use.</returns>
    /// <exception cref="ArgumentException"><paramref name="seed"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="path"/> is not an all-hardened derivation path starting with <c>m</c>.</exception>
    public static byte[] DeriveEd25519(ReadOnlySpan<byte> seed, string path)
    {
        if (seed.IsEmpty)
            throw new ArgumentException("The seed must not be empty.", nameof(seed));

        var indexes = ParsePath(path);

        // HMAC output: the left 32 bytes are the key, the right 32 bytes the chain code (SLIP-0010).
        var current = HMACSHA512.HashData(MasterKey, seed);
        Span<byte> message = stackalloc byte[37];
        try
        {
            foreach (var index in indexes)
            {
                message[0] = 0;
                current.AsSpan(0, 32).CopyTo(message[1..]);
                BinaryPrimitives.WriteUInt32BigEndian(message[33..], index);

                var child = HMACSHA512.HashData(current.AsSpan(32, 32), message);
                CryptographicOperations.ZeroMemory(current);
                current = child;
            }

            return current.AsSpan(0, 32).ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
            CryptographicOperations.ZeroMemory(message);
        }
    }

    private static uint[] ParsePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var parts = path.Split('/');
        if (parts[0] != "m")
            throw new FormatException("A derivation path must start with 'm', e.g. m/44'/501'/0'/0'.");

        var indexes = new uint[parts.Length - 1];
        for (var n = 1; n < parts.Length; n++)
        {
            var part = parts[n];
            if (part.Length < 2 || part[^1] != '\'')
                throw new FormatException(
                    $"Ed25519 (SLIP-0010) supports hardened derivation only; write \"{part}'\" instead of \"{part}\".");

            if (!uint.TryParse(part[..^1], out var index) || index >= HardenedOffset)
                throw new FormatException($"Invalid derivation index '{part}'.");

            indexes[n - 1] = index | HardenedOffset;
        }

        return indexes;
    }
}
