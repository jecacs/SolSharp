using System.Security.Cryptography;
using System.Text;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// Derives program-derived addresses (PDAs): deterministic, off-curve addresses owned by a program that
/// have no corresponding private key.
/// </summary>
public static class ProgramDerivedAddress
{
    /// <summary>The maximum length, in bytes, of a single PDA seed.</summary>
    public const int MaxSeedLength = 32;

    /// <summary>The maximum number of seeds a PDA derivation accepts (16). The bump seed counts toward the limit.</summary>
    public const int MaxSeeds = 16;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static ReadOnlySpan<byte> Marker => "ProgramDerivedAddress"u8;

    /// <summary>
    /// Derives an address for a base account, UTF-8 seed, and owner using Solana's
    /// <c>create_with_seed</c> SHA-256 construction. Unlike a PDA, the result may be on the curve.
    /// </summary>
    /// <param name="baseAddress">The base account whose authority can create the derived account.</param>
    /// <param name="seed">The UTF-8 seed, at most <see cref="MaxSeedLength"/> encoded bytes.</param>
    /// <param name="owner">The program that will own the derived account.</param>
    /// <returns>The SHA-256 hash of the base address, UTF-8 seed bytes, and owner address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="seed"/> is invalid UTF-16 or exceeds <see cref="MaxSeedLength"/> UTF-8 bytes, or the
    /// owner bytes end with Solana's reserved <c>ProgramDerivedAddress</c> marker.
    /// </exception>
    public static PublicKey CreateWithSeed(PublicKey baseAddress, string seed, PublicKey owner)
    {
        ArgumentNullException.ThrowIfNull(seed);

        int seedLength;
        try
        {
            seedLength = StrictUtf8.GetByteCount(seed);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("The derived-address seed must contain valid UTF-16 text.", nameof(seed), exception);
        }

        if (seedLength > MaxSeedLength)
            throw new ArgumentException(
                $"A derived-address seed may be at most {MaxSeedLength} UTF-8 bytes, got {seedLength}.",
                nameof(seed));

        Span<byte> input = stackalloc byte[PublicKey.Length + seedLength + PublicKey.Length];
        baseAddress.CopyTo(input);
        StrictUtf8.GetBytes(seed, input.Slice(PublicKey.Length, seedLength));

        var ownerBytes = input[(PublicKey.Length + seedLength)..];
        owner.CopyTo(ownerBytes);
        if (ownerBytes.EndsWith(Marker))
            throw new ArgumentException(
                "The owner address ends with Solana's reserved ProgramDerivedAddress marker.",
                nameof(owner));

        Span<byte> hash = stackalloc byte[PublicKey.Length];
        SHA256.HashData(input, hash);
        return new(hash);
    }

    /// <summary>
    /// Derives the canonical PDA for <paramref name="seeds"/> under <paramref name="programId"/>, trying bump
    /// seeds from 255 downward and returning the first that produces an off-curve address.
    /// </summary>
    /// <param name="seeds">The seeds; each may be at most <see cref="MaxSeedLength"/> bytes, and at most <see cref="MaxSeeds"/> - 1 of them (the bump occupies the last slot).</param>
    /// <param name="programId">The program the address is derived for.</param>
    /// <returns>The derived address and the bump seed that produced it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A seed exceeds <see cref="MaxSeedLength"/> bytes, or the seeds plus the bump exceed <see cref="MaxSeeds"/>.</exception>
    /// <exception cref="InvalidOperationException">No bump seed produced an off-curve address (cryptographically improbable).</exception>
    public static (PublicKey Address, byte Bump) FindProgramAddress(IReadOnlyList<byte[]> seeds, PublicKey programId)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var seedCount = seeds.Count;
        if (seedCount >= MaxSeeds)
            throw new ArgumentException(
                $"Finding a PDA accepts at most {MaxSeeds - 1} caller seed(s), got {seedCount}; the bump occupies the final slot.",
                nameof(seeds));

        var withBump = new byte[seedCount + 1][];
        for (var i = 0; i < seedCount; i++)
        {
            var seed = seeds[i];
            ArgumentNullException.ThrowIfNull(seed, nameof(seeds));
            withBump[i] = seed;
        }

        for (var bump = 255; bump >= 0; bump--)
        {
            withBump[^1] = [(byte)bump];
            if (TryCreateProgramAddress(withBump, programId, out var address))
                return (address, (byte)bump);
        }

        throw new InvalidOperationException("No bump seed produced an off-curve address.");
    }

    /// <summary>
    /// Derives the PDA for an exact seed set, with no bump search. Fails if the resulting address happens to
    /// fall on the curve (in which case a different seed or bump is needed).
    /// </summary>
    /// <param name="seeds">The seeds; at most <see cref="MaxSeeds"/> of them, each at most <see cref="MaxSeedLength"/> bytes.</param>
    /// <param name="programId">The program the address is derived for.</param>
    /// <param name="address">The derived off-curve address on success; <see langword="default"/> otherwise.</param>
    /// <returns><c>true</c> if the seeds produced a valid off-curve address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">More than <see cref="MaxSeeds"/> seeds, or a seed exceeds <see cref="MaxSeedLength"/> bytes.</exception>
    public static bool TryCreateProgramAddress(IReadOnlyList<byte[]> seeds, PublicKey programId, out PublicKey address)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        // solana-sdk enforces MAX_SEEDS in create_program_address; without this check the derivation would
        // happily produce an address the runtime rejects.
        if (seeds.Count > MaxSeeds)
            throw new ArgumentException($"A PDA derivation accepts at most {MaxSeeds} seeds, got {seeds.Count}.", nameof(seeds));

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var seed in seeds)
        {
            ArgumentNullException.ThrowIfNull(seed, nameof(seeds));
            if (seed.Length > MaxSeedLength)
                throw new ArgumentException($"A PDA seed may be at most {MaxSeedLength} bytes, got {seed.Length}.", nameof(seeds));

            hasher.AppendData(seed);
        }

        hasher.AppendData(programId.ToBytes());
        hasher.AppendData(Marker);

        var candidate = new PublicKey(hasher.GetHashAndReset());
        if (candidate.IsOnCurve())
        {
            address = default;
            return false;
        }

        address = candidate;
        return true;
    }
}
