namespace SolSharp.Core;

/// <summary>Conversions between SOL and lamports, the integer base unit (1 SOL = 1,000,000,000 lamports).</summary>
public static class SolanaUnits
{
    /// <summary>The number of lamports in one SOL.</summary>
    public const ulong LamportsPerSol = 1_000_000_000;

    /// <summary>Converts an amount of SOL to lamports, truncating any sub-lamport fraction toward zero.</summary>
    /// <param name="sol">The amount in SOL; must not be negative.</param>
    /// <returns>The amount in lamports.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sol"/> is negative, or converts to more than <see cref="ulong.MaxValue"/> lamports.</exception>
    public static ulong SolToLamports(decimal sol)
    {
        if (sol < 0)
            throw new ArgumentOutOfRangeException(nameof(sol), sol, "SOL amount cannot be negative.");

        var lamports = sol * LamportsPerSol;
        if (lamports > ulong.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(sol), sol, "SOL amount is too large to express in lamports.");

        return (ulong)lamports;
    }

    /// <summary>Converts an amount of lamports to SOL.</summary>
    /// <param name="lamports">The amount in lamports.</param>
    /// <returns>The amount in SOL.</returns>
    public static decimal LamportsToSol(ulong lamports) => lamports / (decimal)LamportsPerSol;
}
