using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Identifies which stake-account authority is being changed.</summary>
public enum StakeAuthorityType : uint
{
    /// <summary>The authority that delegates, deactivates, splits, merges, or moves stake.</summary>
    Staker = 0,

    /// <summary>The authority that withdraws funds and can change either authority.</summary>
    Withdrawer = 1
}

/// <summary>The two authorities stored in an initialized stake account.</summary>
/// <param name="Staker">The stake-management authority.</param>
/// <param name="Withdrawer">The withdrawal authority.</param>
public readonly record struct StakeAuthorized(PublicKey Staker, PublicKey Withdrawer)
{
    /// <summary>Creates an authority pair that uses one key for both roles.</summary>
    /// <param name="authority">The key to use for both roles.</param>
    /// <returns>The authority pair.</returns>
    public static StakeAuthorized ForSingleAuthority(PublicKey authority) => new(authority, authority);
}

/// <summary>The withdrawal lockup stored in a stake account.</summary>
/// <param name="UnixTimestamp">The earliest permitted withdrawal Unix timestamp.</param>
/// <param name="Epoch">The earliest permitted withdrawal epoch.</param>
/// <param name="Custodian">The authority that may override an active lockup.</param>
public readonly record struct StakeLockup(long UnixTimestamp, ulong Epoch, PublicKey Custodian);

/// <summary>Optional values used to update a stake-account lockup.</summary>
/// <param name="UnixTimestamp">A replacement Unix timestamp, or <c>null</c> to preserve it.</param>
/// <param name="Epoch">A replacement epoch, or <c>null</c> to preserve it.</param>
/// <param name="Custodian">A replacement custodian, or <c>null</c> to preserve it.</param>
public readonly record struct StakeLockupArguments(long? UnixTimestamp, ulong? Epoch, PublicKey? Custodian);

/// <summary>The serialized stake-account state variant.</summary>
public enum StakeAccountStateKind : uint
{
    /// <summary>The account has not been initialized.</summary>
    Uninitialized = 0,

    /// <summary>The account has authorities and a lockup but no delegation.</summary>
    Initialized = 1,

    /// <summary>The account contains a stake delegation.</summary>
    Stake = 2,

    /// <summary>The legacy rewards-pool variant.</summary>
    RewardsPool = 3
}

/// <summary>Initialization metadata stored in a stake account.</summary>
/// <param name="RentExemptReserve">The historical rent-exempt reserve captured at initialization.</param>
/// <param name="Authorized">The stake and withdrawal authorities.</param>
/// <param name="Lockup">The account's withdrawal lockup.</param>
public readonly record struct StakeAccountMetadata(
    ulong RentExemptReserve,
    StakeAuthorized Authorized,
    StakeLockup Lockup);

/// <summary>A delegation stored in a delegated stake account.</summary>
/// <param name="Voter">The vote account receiving the delegation.</param>
/// <param name="Lamports">The delegated lamports.</param>
/// <param name="ActivationEpoch">The epoch in which activation began.</param>
/// <param name="DeactivationEpoch">The epoch in which deactivation began, or <see cref="ulong.MaxValue"/>.</param>
/// <param name="Reserved">The reserved 64-bit compatibility field.</param>
public readonly record struct StakeDelegation(
    PublicKey Voter,
    ulong Lamports,
    ulong ActivationEpoch,
    ulong DeactivationEpoch,
    ulong Reserved);

/// <summary>The delegation and observed vote credits stored in a stake account.</summary>
/// <param name="Delegation">The stake delegation.</param>
/// <param name="CreditsObserved">Vote credits observed when the stake was delegated or redeemed.</param>
public readonly record struct StakeDelegatedData(StakeDelegation Delegation, ulong CreditsObserved);
