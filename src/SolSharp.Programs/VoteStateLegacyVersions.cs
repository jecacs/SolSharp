using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>A decoded vote state using the V1.14.11 layout.</summary>
public sealed record VoteStateV1_14_11 : VoteStateVersions
{
    /// <summary>The exact pinned vote-account allocation.</summary>
    public const int DataLength = 3_731;

    private VoteStateV1_14_11(
        PublicKey node,
        PublicKey authorizedWithdrawer,
        byte commission,
        IReadOnlyList<VoteStateLockout> votes,
        ulong? rootSlot,
        IReadOnlyList<AuthorizedVoteVoter> authorizedVoters,
        IReadOnlyList<PriorVoteVoter> priorVoters,
        int priorVoterIndex,
        bool priorVotersEmpty,
        IReadOnlyList<VoteEpochCredits> epochCredits,
        VoteStateTimestamp lastTimestamp)
    {
        Node = node;
        AuthorizedWithdrawer = authorizedWithdrawer;
        Commission = commission;
        Votes = votes;
        RootSlot = rootSlot;
        AuthorizedVoters = authorizedVoters;
        PriorVoters = priorVoters;
        PriorVoterIndex = priorVoterIndex;
        PriorVotersEmpty = priorVotersEmpty;
        EpochCredits = epochCredits;
        LastTimestamp = lastTimestamp;
    }

    /// <inheritdoc/>
    public override VoteStateVersion Version => VoteStateVersion.V1_14_11;

    /// <inheritdoc/>
    public override PublicKey Node { get; }

    /// <inheritdoc/>
    public override PublicKey AuthorizedWithdrawer { get; }

    /// <summary>The legacy reward commission percentage.</summary>
    public byte Commission { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteStateLockout> Votes { get; }

    /// <inheritdoc/>
    public override ulong? RootSlot { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AuthorizedVoteVoter> AuthorizedVoters { get; }

    /// <summary>All 32 serialized entries of the prior-voter circular buffer.</summary>
    public IReadOnlyList<PriorVoteVoter> PriorVoters { get; }

    /// <summary>The next position recorded by the prior-voter circular buffer.</summary>
    public int PriorVoterIndex { get; }

    /// <summary>Whether the prior-voter circular buffer is empty.</summary>
    public bool PriorVotersEmpty { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteEpochCredits> EpochCredits { get; }

    /// <inheritdoc/>
    public override VoteStateTimestamp LastTimestamp { get; }

    /// <inheritdoc/>
    public override bool IsUninitialized => AuthorizedVoters.Count is 0;

    /// <summary>Checks the V1.14.11 exact allocation and initialization sentinel used by the pinned SDK.</summary>
    /// <param name="data">The complete account allocation.</param>
    /// <returns><c>true</c> when the allocation has the exact size and nonzero initialization prefix.</returns>
    public static new bool IsCorrectSizeAndInitialized(ReadOnlySpan<byte> data) =>
        data.Length == DataLength && HasAnyNonzero(data, sizeof(uint), 82);

    internal static VoteStateV1_14_11 ParseBody(ref VoteStateBincodeReader reader)
    {
        var node = reader.ReadPublicKey();
        var withdrawer = reader.ReadPublicKey();
        var commission = reader.ReadByte();
        var votes = ReadVotes(ref reader, hasLatency: false);
        var root = reader.ReadOptionalUInt64();
        var authorizedVoters = ReadAuthorizedVoters(ref reader);
        var priorVoters = ReadPriorVoters(ref reader, out var priorVoterIndex, out var priorVotersEmpty);
        var epochCredits = ReadEpochCredits(ref reader);
        var timestamp = ReadTimestamp(ref reader);
        return new VoteStateV1_14_11(
            node,
            withdrawer,
            commission,
            votes,
            root,
            authorizedVoters,
            priorVoters,
            priorVoterIndex,
            priorVotersEmpty,
            epochCredits,
            timestamp);
    }
}

/// <summary>A decoded vote state using the latency-aware V3 layout.</summary>
public sealed record VoteStateV3 : VoteStateVersions
{
    /// <summary>The exact pinned vote-account allocation.</summary>
    public const int DataLength = 3_762;

    private VoteStateV3(
        PublicKey node,
        PublicKey authorizedWithdrawer,
        byte commission,
        IReadOnlyList<VoteStateLockout> votes,
        ulong? rootSlot,
        IReadOnlyList<AuthorizedVoteVoter> authorizedVoters,
        IReadOnlyList<PriorVoteVoter> priorVoters,
        int priorVoterIndex,
        bool priorVotersEmpty,
        IReadOnlyList<VoteEpochCredits> epochCredits,
        VoteStateTimestamp lastTimestamp)
    {
        Node = node;
        AuthorizedWithdrawer = authorizedWithdrawer;
        Commission = commission;
        Votes = votes;
        RootSlot = rootSlot;
        AuthorizedVoters = authorizedVoters;
        PriorVoters = priorVoters;
        PriorVoterIndex = priorVoterIndex;
        PriorVotersEmpty = priorVotersEmpty;
        EpochCredits = epochCredits;
        LastTimestamp = lastTimestamp;
    }

    /// <inheritdoc/>
    public override VoteStateVersion Version => VoteStateVersion.V3;

    /// <inheritdoc/>
    public override PublicKey Node { get; }

    /// <inheritdoc/>
    public override PublicKey AuthorizedWithdrawer { get; }

    /// <summary>The legacy reward commission percentage.</summary>
    public byte Commission { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteStateLockout> Votes { get; }

    /// <inheritdoc/>
    public override ulong? RootSlot { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AuthorizedVoteVoter> AuthorizedVoters { get; }

    /// <summary>All 32 serialized entries of the prior-voter circular buffer.</summary>
    public IReadOnlyList<PriorVoteVoter> PriorVoters { get; }

    /// <summary>The next position recorded by the prior-voter circular buffer.</summary>
    public int PriorVoterIndex { get; }

    /// <summary>Whether the prior-voter circular buffer is empty.</summary>
    public bool PriorVotersEmpty { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<VoteEpochCredits> EpochCredits { get; }

    /// <inheritdoc/>
    public override VoteStateTimestamp LastTimestamp { get; }

    /// <inheritdoc/>
    public override bool IsUninitialized => AuthorizedVoters.Count is 0;

    /// <summary>Checks the V3 exact allocation and initialization sentinel used by the pinned SDK.</summary>
    /// <param name="data">The complete account allocation.</param>
    /// <returns><c>true</c> when the allocation has the exact size and nonzero initialization prefix.</returns>
    public static new bool IsCorrectSizeAndInitialized(ReadOnlySpan<byte> data) =>
        data.Length == DataLength && HasAnyNonzero(data, sizeof(uint), 114);

    internal static VoteStateV3 ParseBody(ref VoteStateBincodeReader reader)
    {
        var node = reader.ReadPublicKey();
        var withdrawer = reader.ReadPublicKey();
        var commission = reader.ReadByte();
        var votes = ReadVotes(ref reader, hasLatency: true);
        var root = reader.ReadOptionalUInt64();
        var authorizedVoters = ReadAuthorizedVoters(ref reader);
        var priorVoters = ReadPriorVoters(ref reader, out var priorVoterIndex, out var priorVotersEmpty);
        var epochCredits = ReadEpochCredits(ref reader);
        var timestamp = ReadTimestamp(ref reader);
        return new VoteStateV3(
            node,
            withdrawer,
            commission,
            votes,
            root,
            authorizedVoters,
            priorVoters,
            priorVoterIndex,
            priorVotersEmpty,
            epochCredits,
            timestamp);
    }
}
