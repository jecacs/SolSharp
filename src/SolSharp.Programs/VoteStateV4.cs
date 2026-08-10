using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>A decoded vote state using the collector- and BLS-aware V4 layout.</summary>
public sealed record VoteStateV4 : VoteStateVersions
{
    /// <summary>The exact pinned vote-account allocation, retained from V3.</summary>
    public const int DataLength = 3_762;

    private VoteStateV4(
        PublicKey node,
        PublicKey authorizedWithdrawer,
        PublicKey inflationRewardsCollector,
        PublicKey blockRevenueCollector,
        ushort inflationRewardsCommissionBasisPoints,
        ushort blockRevenueCommissionBasisPoints,
        ulong pendingDelegatorRewards,
        ReadOnlyMemory<byte>? blsPublicKey,
        IReadOnlyList<VoteStateLockout> votes,
        ulong? rootSlot,
        IReadOnlyList<AuthorizedVoteVoter> authorizedVoters,
        IReadOnlyList<VoteEpochCredits> epochCredits,
        VoteStateTimestamp lastTimestamp)
    {
        Node = node;
        AuthorizedWithdrawer = authorizedWithdrawer;
        InflationRewardsCollector = inflationRewardsCollector;
        BlockRevenueCollector = blockRevenueCollector;
        InflationRewardsCommissionBasisPoints = inflationRewardsCommissionBasisPoints;
        BlockRevenueCommissionBasisPoints = blockRevenueCommissionBasisPoints;
        PendingDelegatorRewards = pendingDelegatorRewards;
        BlsPublicKey = blsPublicKey;
        Votes = votes;
        RootSlot = rootSlot;
        AuthorizedVoters = authorizedVoters;
        EpochCredits = epochCredits;
        LastTimestamp = lastTimestamp;
    }

    /// <inheritdoc/>
    public override VoteStateVersion Version => VoteStateVersion.V4;

    /// <inheritdoc/>
    public override PublicKey Node { get; }

    /// <inheritdoc/>
    public override PublicKey AuthorizedWithdrawer { get; }

    /// <summary>The collector of inflation rewards.</summary>
    public PublicKey InflationRewardsCollector { get; }

    /// <summary>The collector of block revenue.</summary>
    public PublicKey BlockRevenueCollector { get; }

    /// <summary>The inflation-reward commission in basis points.</summary>
    public ushort InflationRewardsCommissionBasisPoints { get; }

    /// <summary>The block-revenue commission in basis points.</summary>
    public ushort BlockRevenueCommissionBasisPoints { get; }

    /// <summary>The rewards pending distribution to stake delegators.</summary>
    public ulong PendingDelegatorRewards { get; }

    /// <summary>The optional 48-byte compressed BLS public key.</summary>
    public ReadOnlyMemory<byte>? BlsPublicKey { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteStateLockout> Votes { get; }

    /// <inheritdoc/>
    public override ulong? RootSlot { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AuthorizedVoteVoter> AuthorizedVoters { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteEpochCredits> EpochCredits { get; }

    /// <inheritdoc/>
    public override VoteStateTimestamp LastTimestamp { get; }

    /// <inheritdoc/>
    public override bool IsUninitialized => false;

    /// <summary>Checks the exact V4 allocation and its raw little-endian discriminant.</summary>
    /// <param name="data">The complete account allocation.</param>
    /// <returns><c>true</c> when the allocation has the exact size and V4 discriminant.</returns>
    public static new bool IsCorrectSizeAndInitialized(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> tag = [3, 0, 0, 0];
        return data.Length == DataLength && data[..sizeof(uint)].SequenceEqual(tag);
    }

    internal static VoteStateV4 ParseBody(ref VoteStateBincodeReader reader)
    {
        var node = reader.ReadPublicKey();
        var withdrawer = reader.ReadPublicKey();
        var inflationCollector = reader.ReadPublicKey();
        var blockCollector = reader.ReadPublicKey();
        var inflationCommission = reader.ReadUInt16();
        var blockCommission = reader.ReadUInt16();
        var pendingRewards = reader.ReadUInt64();
        var blsPublicKey = reader.ReadOptionalBlsPublicKey();
        var votes = ReadVotes(ref reader, hasLatency: true);
        var root = reader.ReadOptionalUInt64();
        var authorizedVoters = ReadAuthorizedVoters(ref reader);
        var epochCredits = ReadEpochCredits(ref reader);
        var timestamp = ReadTimestamp(ref reader);
        return new(
            node,
            withdrawer,
            inflationCollector,
            blockCollector,
            inflationCommission,
            blockCommission,
            pendingRewards,
            blsPublicKey,
            votes,
            root,
            authorizedVoters,
            epochCredits,
            timestamp);
    }
}
