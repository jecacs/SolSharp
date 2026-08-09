namespace SolSharp.Core.Constants;

/// <summary>Well-known Solana sysvar account addresses (base58).</summary>
public static class Sysvars
{
    /// <summary>The owner assigned to every sysvar account.</summary>
    public const string Owner = "Sysvar1111111111111111111111111111111111111";

    /// <summary>The Rent sysvar: the rent rate and exemption threshold.</summary>
    public const string Rent = "SysvarRent111111111111111111111111111111111";

    /// <summary>The Clock sysvar: the current slot, epoch, and unix timestamp.</summary>
    public const string Clock = "SysvarC1ock11111111111111111111111111111111";

    /// <summary>The current epoch-rewards distribution sysvar.</summary>
    public const string EpochRewards = "SysvarEpochRewards1111111111111111111111111";

    /// <summary>The epoch schedule sysvar.</summary>
    public const string EpochSchedule = "SysvarEpochSchedu1e111111111111111111111111";

    /// <summary>The deprecated fees sysvar still exported by the current SDK.</summary>
    public const string Fees = "SysvarFees111111111111111111111111111111111";

    /// <summary>The Instructions sysvar: introspection into the current transaction's instructions.</summary>
    public const string Instructions = "Sysvar1nstructions1111111111111111111111111";

    /// <summary>The RecentBlockhashes sysvar (deprecated on-chain, still referenced by older programs).</summary>
    public const string RecentBlockhashes = "SysvarRecentB1ockHashes11111111111111111111";

    /// <summary>The last cluster restart slot sysvar.</summary>
    public const string LastRestartSlot = "SysvarLastRestartS1ot1111111111111111111111";

    /// <summary>The deprecated rewards sysvar still exported by the current SDK.</summary>
    public const string Rewards = "SysvarRewards111111111111111111111111111111";

    /// <summary>The recent slot hashes sysvar.</summary>
    public const string SlotHashes = "SysvarS1otHashes111111111111111111111111111";

    /// <summary>The slot history sysvar.</summary>
    public const string SlotHistory = "SysvarS1otHistory11111111111111111111111111";

    /// <summary>The stake activation history sysvar.</summary>
    public const string StakeHistory = "SysvarStakeHistory1111111111111111111111111";
}
