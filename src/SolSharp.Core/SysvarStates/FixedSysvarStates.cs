using SolSharp.Core.Primitives;

namespace SolSharp.Core.SysvarStates;

/// <summary>The bincode state of the clock sysvar.</summary>
/// <param name="Slot">The current slot.</param>
/// <param name="EpochStartTimestamp">The Unix timestamp of the epoch's first slot.</param>
/// <param name="Epoch">The current epoch.</param>
/// <param name="LeaderScheduleEpoch">The latest epoch with a calculated leader schedule.</param>
/// <param name="UnixTimestamp">The approximate Unix timestamp of the current slot.</param>
public readonly record struct ClockSysvarState(
    ulong Slot,
    long EpochStartTimestamp,
    ulong Epoch,
    ulong LeaderScheduleEpoch,
    long UnixTimestamp)
{
    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 40;

    /// <summary>Decodes the exact bincode representation of a clock sysvar account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded clock.</returns>
    /// <exception cref="ArgumentException">The data is truncated or has trailing bytes.</exception>
    public static ClockSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var state = new ClockSysvarState(
            reader.ReadUInt64(),
            reader.ReadInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadInt64());
        reader.EnsureEnd();
        return state;
    }
}

/// <summary>The current wire fields of the rent sysvar, including the retained deprecated fields.</summary>
/// <param name="LamportsPerByte">The rent-exemption rate in lamports per account byte.</param>
/// <param name="ExemptionThreshold">The threshold encoded by the retained little-endian IEEE-754 field.</param>
/// <param name="BurnPercent">The retained burn percentage field.</param>
public readonly record struct RentSysvarState(
    ulong LamportsPerByte,
    double ExemptionThreshold,
    byte BurnPercent)
{
    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 17;

    /// <summary>Decodes the exact bincode representation of a rent sysvar account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded rent configuration.</returns>
    /// <exception cref="ArgumentException">The data is truncated or has trailing bytes.</exception>
    public static RentSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var state = new RentSysvarState(reader.ReadUInt64(), reader.ReadDouble(), reader.ReadByte());
        reader.EnsureEnd();
        return state;
    }
}

/// <summary>The bincode state of the epoch-schedule sysvar.</summary>
/// <param name="SlotsPerEpoch">The maximum slots in an epoch.</param>
/// <param name="LeaderScheduleSlotOffset">The leader-schedule calculation offset.</param>
/// <param name="Warmup">Whether epoch lengths grow during warmup.</param>
/// <param name="FirstNormalEpoch">The first epoch after warmup.</param>
/// <param name="FirstNormalSlot">The first slot after warmup.</param>
public readonly record struct EpochScheduleSysvarState(
    ulong SlotsPerEpoch,
    ulong LeaderScheduleSlotOffset,
    bool Warmup,
    ulong FirstNormalEpoch,
    ulong FirstNormalSlot)
{
    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 33;

    /// <summary>Decodes the exact bincode representation of an epoch-schedule sysvar account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded epoch schedule.</returns>
    /// <exception cref="ArgumentException">The data is malformed, truncated, or has trailing bytes.</exception>
    public static EpochScheduleSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var state = new EpochScheduleSysvarState(
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadBool(),
            reader.ReadUInt64(),
            reader.ReadUInt64());
        reader.EnsureEnd();
        return state;
    }
}

/// <summary>The bincode state of the epoch-rewards sysvar.</summary>
/// <param name="DistributionStartingBlockHeight">The first block height of the distribution.</param>
/// <param name="NumberOfPartitions">The number of distribution partitions.</param>
/// <param name="ParentBlockhash">The parent hash used to seed partitioning.</param>
/// <param name="TotalPoints">The total calculated reward points.</param>
/// <param name="TotalRewards">The total epoch rewards in lamports.</param>
/// <param name="DistributedRewards">The rewards distributed so far in lamports.</param>
/// <param name="Active">Whether reward calculation or distribution is active.</param>
public readonly record struct EpochRewardsSysvarState(
    ulong DistributionStartingBlockHeight,
    ulong NumberOfPartitions,
    Hash ParentBlockhash,
    UInt128 TotalPoints,
    ulong TotalRewards,
    ulong DistributedRewards,
    bool Active)
{
    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 81;

    /// <summary>Decodes the exact bincode representation of an epoch-rewards sysvar account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded epoch rewards.</returns>
    /// <exception cref="ArgumentException">The data is malformed, truncated, or has trailing bytes.</exception>
    public static EpochRewardsSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var state = new EpochRewardsSysvarState(
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadHash(),
            reader.ReadUInt128(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadBool());
        reader.EnsureEnd();
        return state;
    }
}

/// <summary>The bincode state of the last-restart-slot sysvar.</summary>
/// <param name="LastRestartSlot">The last hard-fork restart slot.</param>
public readonly record struct LastRestartSlotSysvarState(ulong LastRestartSlot)
{
    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 8;

    /// <summary>Decodes the exact bincode representation of a last-restart-slot sysvar account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded restart slot.</returns>
    /// <exception cref="ArgumentException">The data is truncated or has trailing bytes.</exception>
    public static LastRestartSlotSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var state = new LastRestartSlotSysvarState(reader.ReadUInt64());
        reader.EnsureEnd();
        return state;
    }
}
