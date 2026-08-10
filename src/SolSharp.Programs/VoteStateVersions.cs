using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The supported discriminants of the pinned vote-interface <c>VoteStateVersions</c> enum.</summary>
public enum VoteStateVersion : uint
{
    /// <summary>The legacy 1.14.11 state layout.</summary>
    V1_14_11 = 1,

    /// <summary>The latency-aware V3 state layout.</summary>
    V3 = 2,

    /// <summary>The collector- and BLS-aware V4 state layout.</summary>
    V4 = 3
}

/// <summary>A vote lockout decoded from the vote-account state.</summary>
/// <param name="Latency">The landing latency; zero for the V1.14.11 layout.</param>
/// <param name="Slot">The voted slot.</param>
/// <param name="ConfirmationCount">The tower confirmation count.</param>
public readonly record struct VoteStateLockout(byte Latency, ulong Slot, uint ConfirmationCount);

/// <summary>An epoch-keyed authorized vote signer.</summary>
/// <param name="Epoch">The epoch at which this voter became authorized.</param>
/// <param name="Voter">The authorized voter address.</param>
public readonly record struct AuthorizedVoteVoter(ulong Epoch, PublicKey Voter);

/// <summary>A fixed circular-buffer entry for a prior vote authority.</summary>
/// <param name="Voter">The prior authority.</param>
/// <param name="FromEpoch">The inclusive first authorized epoch.</param>
/// <param name="UntilEpoch">The exclusive last authorized epoch.</param>
public readonly record struct PriorVoteVoter(PublicKey Voter, ulong FromEpoch, ulong UntilEpoch);

/// <summary>Vote credits accumulated for an epoch.</summary>
/// <param name="Epoch">The epoch.</param>
/// <param name="Credits">The total credits at the end of the epoch.</param>
/// <param name="PreviousCredits">The credits carried into the epoch.</param>
public readonly record struct VoteEpochCredits(ulong Epoch, ulong Credits, ulong PreviousCredits);

/// <summary>The most recent timestamp submitted to a vote account.</summary>
/// <param name="Slot">The timestamped slot.</param>
/// <param name="UnixTimestamp">The submitted Unix timestamp.</param>
public readonly record struct VoteStateTimestamp(ulong Slot, long UnixTimestamp);

/// <summary>A strictly bounded decoded variant of the pinned vote-interface <c>VoteStateVersions</c>.</summary>
public abstract record VoteStateVersions
{
    /// <summary>The maximum number of tower lockouts retained by the runtime.</summary>
    public const int MaximumLockouts = 31;

    /// <summary>The maximum number of epoch-credit entries retained by the runtime.</summary>
    public const int MaximumEpochCredits = 64;

    /// <summary>The fixed number of prior-voter circular-buffer entries in V1.14.11 and V3.</summary>
    public const int PriorVoterEntries = 32;

    /// <summary>The maximum serialized authorized-voter entries used by current vote states.</summary>
    public const int MaximumAuthorizedVoters = 4;

    /// <summary>The compressed BLS public-key length.</summary>
    public const int BlsPublicKeyLength = 48;

    /// <summary>The raw little-endian bincode enum tag.</summary>
    public abstract VoteStateVersion Version { get; }

    /// <summary>The validator identity.</summary>
    public abstract PublicKey Node { get; }

    /// <summary>The vote account's withdrawal authority.</summary>
    public abstract PublicKey AuthorizedWithdrawer { get; }

    /// <summary>The ordered tower lockouts.</summary>
    public abstract IReadOnlyList<VoteStateLockout> Votes { get; }

    /// <summary>The rooted slot, when present.</summary>
    public abstract ulong? RootSlot { get; }

    /// <summary>The epoch-keyed authorized voters.</summary>
    public abstract IReadOnlyList<AuthorizedVoteVoter> AuthorizedVoters { get; }

    /// <summary>The vote-credit history.</summary>
    public abstract IReadOnlyList<VoteEpochCredits> EpochCredits { get; }

    /// <summary>The last submitted timestamp.</summary>
    public abstract VoteStateTimestamp LastTimestamp { get; }

    /// <summary>Whether the decoded variant is the vote-program's uninitialized sentinel.</summary>
    public abstract bool IsUninitialized { get; }

    /// <summary>Decodes a V1.14.11, V3, or V4 vote account without coercing its version.</summary>
    /// <param name="data">Vote-account data beginning with the four-byte bincode enum discriminant.</param>
    /// <returns>The exact decoded variant.</returns>
    /// <exception cref="ArgumentException">The tag, fields, option values, or bounded collection counts are invalid.</exception>
    public static VoteStateVersions Parse(ReadOnlySpan<byte> data)
    {
        var reader = new VoteStateBincodeReader(data);
        var tag = reader.ReadUInt32();
        return tag switch
        {
            (uint)VoteStateVersion.V1_14_11 => VoteStateV1_14_11.ParseBody(ref reader),
            (uint)VoteStateVersion.V3 => VoteStateV3.ParseBody(ref reader),
            (uint)VoteStateVersion.V4 => VoteStateV4.ParseBody(ref reader),
            _ => throw new ArgumentException($"Unsupported vote-state tag {tag}.", nameof(data))
        };
    }

    /// <summary>Checks the exact account allocation and upstream initialization sentinel without decoding collections.</summary>
    /// <param name="data">The complete vote-account data allocation.</param>
    /// <returns><c>true</c> for an initialized V1.14.11, V3, or V4 allocation of the exact pinned size.</returns>
    public static bool IsCorrectSizeAndInitialized(ReadOnlySpan<byte> data) =>
        VoteStateV4.IsCorrectSizeAndInitialized(data) ||
        VoteStateV3.IsCorrectSizeAndInitialized(data) ||
        VoteStateV1_14_11.IsCorrectSizeAndInitialized(data);

    internal static IReadOnlyList<VoteStateLockout> ReadVotes(ref VoteStateBincodeReader reader, bool hasLatency)
    {
        var count = reader.ReadBoundedCount(MaximumLockouts, "Vote lockout");
        var votes = new VoteStateLockout[count];
        for (var i = 0; i < votes.Length; i++)
        {
            var latency = hasLatency ? reader.ReadByte() : (byte)0;
            votes[i] = new(latency, reader.ReadUInt64(), reader.ReadUInt32());
        }

        return votes;
    }

    internal static IReadOnlyList<AuthorizedVoteVoter> ReadAuthorizedVoters(ref VoteStateBincodeReader reader)
    {
        var count = reader.ReadBoundedCount(MaximumAuthorizedVoters, "Authorized voter");
        var voters = new AuthorizedVoteVoter[count];
        for (var i = 0; i < voters.Length; i++)
            voters[i] = new(reader.ReadUInt64(), reader.ReadPublicKey());
        return voters;
    }

    internal static IReadOnlyList<PriorVoteVoter> ReadPriorVoters(
        ref VoteStateBincodeReader reader,
        out int index,
        out bool isEmpty)
    {
        var voters = new PriorVoteVoter[PriorVoterEntries];
        for (var i = 0; i < voters.Length; i++)
            voters[i] = new(reader.ReadPublicKey(), reader.ReadUInt64(), reader.ReadUInt64());

        var rawIndex = reader.ReadUInt64();
        if (rawIndex >= PriorVoterEntries)
            throw new ArgumentException($"Prior-voter index {rawIndex} is outside the fixed circular buffer.");

        index = (int)rawIndex;
        isEmpty = reader.ReadBool();
        return voters;
    }

    internal static IReadOnlyList<VoteEpochCredits> ReadEpochCredits(ref VoteStateBincodeReader reader)
    {
        var count = reader.ReadBoundedCount(MaximumEpochCredits, "Epoch credits");
        var credits = new VoteEpochCredits[count];
        for (var i = 0; i < credits.Length; i++)
            credits[i] = new(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
        return credits;
    }

    internal static VoteStateTimestamp ReadTimestamp(ref VoteStateBincodeReader reader) =>
        new(reader.ReadUInt64(), reader.ReadInt64());

    internal static bool HasAnyNonzero(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (data.Length < offset + length)
            return false;

        foreach (var value in data.Slice(offset, length))
        {
            if (value is not 0)
                return true;
        }

        return false;
    }
}
