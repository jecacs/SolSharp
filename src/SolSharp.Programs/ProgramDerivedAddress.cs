using System.Security.Cryptography;
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

    private static ReadOnlySpan<byte> Marker => "ProgramDerivedAddress"u8;

    /// <summary>
    /// Derives the canonical PDA for <paramref name="seeds"/> under <paramref name="programId"/>, trying bump
    /// seeds from 255 downward and returning the first that produces an off-curve address.
    /// </summary>
    /// <param name="seeds">The seeds; each may be at most <see cref="MaxSeedLength"/> bytes, and at most <see cref="MaxSeeds"/> - 1 of them (the bump occupies the last slot).</param>
    /// <param name="programId">The program the address is derived for.</param>
    /// <returns>The derived address and the bump seed that produced it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A seed exceeds <see cref="MaxSeedLength"/> bytes, or the seeds plus the bump exceed <see cref="MaxSeeds"/>.</exception>
    /// <exception cref="InvalidOperationException">No bump seed produced an off-curve address (cryptographically improbable).</exception>
    public static (PublicKey Address, byte Bump) FindProgramAddress(IReadOnlyList<byte[]> seeds, PublicKey programId)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        var withBump = new byte[seeds.Count + 1][];
        for (var i = 0; i < seeds.Count; i++)
            withBump[i] = seeds[i];

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
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> is <c>null</c>.</exception>
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
