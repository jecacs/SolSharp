using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Decoded contents of a fixed-size Solana stake account (<c>StakeStateV2</c>).</summary>
public sealed class StakeAccountState
{
    /// <summary>The fixed serialized length of every stake account.</summary>
    public const int AccountDataLength = 200;

    private StakeAccountState(
        StakeAccountStateKind kind,
        StakeAccountMetadata? metadata,
        StakeDelegatedData? stake,
        byte stakeFlags)
    {
        Kind = kind;
        Metadata = metadata;
        Stake = stake;
        StakeFlags = stakeFlags;
    }

    /// <summary>The decoded state variant.</summary>
    public StakeAccountStateKind Kind { get; }

    /// <summary>The account metadata for initialized and delegated states.</summary>
    public StakeAccountMetadata? Metadata { get; }

    /// <summary>The delegation for the delegated state.</summary>
    public StakeDelegatedData? Stake { get; }

    /// <summary>The raw <c>StakeFlags</c> byte for the delegated state.</summary>
    public byte StakeFlags { get; }

    /// <summary>Decodes exactly 200 bytes of bincode-compatible <c>StakeStateV2</c> account data.</summary>
    /// <param name="data">The stake account data.</param>
    /// <returns>The decoded state.</returns>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not exactly 200 bytes.</exception>
    /// <exception cref="FormatException">The state discriminator is unknown.</exception>
    public static StakeAccountState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != AccountDataLength)
        {
            throw new ArgumentException(
                $"Stake account data must be exactly {AccountDataLength} bytes, got {data.Length}.",
                nameof(data));
        }

        var kindValue = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (!Enum.IsDefined(typeof(StakeAccountStateKind), kindValue))
            throw new FormatException($"Unknown stake-account state discriminator {kindValue}.");

        var kind = (StakeAccountStateKind)kindValue;
        if (kind is StakeAccountStateKind.Uninitialized or StakeAccountStateKind.RewardsPool)
            return new(kind, null, null, 0);

        var metadata = ReadMetadata(data[sizeof(uint)..]);
        if (kind == StakeAccountStateKind.Initialized)
            return new(kind, metadata, null, 0);

        const int stakeOffset = sizeof(uint) + 120;
        var voter = new PublicKey(data.Slice(stakeOffset, PublicKey.Length));
        var lamports = BinaryPrimitives.ReadUInt64LittleEndian(data[(stakeOffset + 32)..]);
        var activationEpoch = BinaryPrimitives.ReadUInt64LittleEndian(data[(stakeOffset + 40)..]);
        var deactivationEpoch = BinaryPrimitives.ReadUInt64LittleEndian(data[(stakeOffset + 48)..]);
        var reserved = BinaryPrimitives.ReadUInt64LittleEndian(data[(stakeOffset + 56)..]);
        var creditsObserved = BinaryPrimitives.ReadUInt64LittleEndian(data[(stakeOffset + 64)..]);
        var delegation = new StakeDelegation(voter, lamports, activationEpoch, deactivationEpoch, reserved);

        return new(
            kind,
            metadata,
            new StakeDelegatedData(delegation, creditsObserved),
            data[stakeOffset + 72]);
    }

    /// <summary>Attempts to decode a fixed-size <c>StakeStateV2</c> value.</summary>
    /// <param name="data">The stake account data.</param>
    /// <param name="state">The decoded state on success; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the input has the expected length and a known discriminator.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out StakeAccountState? state)
    {
        try
        {
            state = Parse(data);
            return true;
        }
        catch (ArgumentException)
        {
            state = null;
            return false;
        }
        catch (FormatException)
        {
            state = null;
            return false;
        }
    }

    private static StakeAccountMetadata ReadMetadata(ReadOnlySpan<byte> data)
    {
        var rentExemptReserve = BinaryPrimitives.ReadUInt64LittleEndian(data);
        var staker = new PublicKey(data.Slice(8, PublicKey.Length));
        var withdrawer = new PublicKey(data.Slice(40, PublicKey.Length));
        var unixTimestamp = BinaryPrimitives.ReadInt64LittleEndian(data[72..]);
        var epoch = BinaryPrimitives.ReadUInt64LittleEndian(data[80..]);
        var custodian = new PublicKey(data.Slice(88, PublicKey.Length));
        return new(
            rentExemptReserve,
            new(staker, withdrawer),
            new(unixTimestamp, epoch, custodian));
    }
}
