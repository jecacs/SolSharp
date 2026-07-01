namespace SolSharp.Core.Constants;

/// <summary>Well-known Solana sysvar account addresses (base58).</summary>
public static class Sysvars
{
    /// <summary>The Rent sysvar: the rent rate and exemption threshold.</summary>
    public const string Rent = "SysvarRent111111111111111111111111111111111";

    /// <summary>The Clock sysvar: the current slot, epoch, and unix timestamp.</summary>
    public const string Clock = "SysvarC1ock11111111111111111111111111111111";

    /// <summary>The Instructions sysvar: introspection into the current transaction's instructions.</summary>
    public const string Instructions = "Sysvar1nstructions1111111111111111111111111";

    /// <summary>The RecentBlockhashes sysvar (deprecated on-chain, still referenced by older programs).</summary>
    public const string RecentBlockhashes = "SysvarRecentB1ockHashes11111111111111111111";
}
