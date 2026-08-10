using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>The commission stream selected by a vote-program instruction.</summary>
public enum VoteCommissionKind : uint
{
    /// <summary>Inflation rewards paid to the vote account.</summary>
    InflationRewards = 0,

    /// <summary>Block revenue paid to the vote account.</summary>
    BlockRevenue = 1
}

/// <summary>The vote-account authority role selected by an authorization instruction.</summary>
public enum VoteAuthorizationKind : uint
{
    /// <summary>The ordinary vote authority.</summary>
    Voter = 0,

    /// <summary>The withdrawal authority.</summary>
    Withdrawer = 1,

    /// <summary>A vote authority accompanied by a BLS public key and proof of possession.</summary>
    VoterWithBls = 2
}

/// <summary>Initialization values for the legacy vote-account state.</summary>
/// <param name="Node">The validator identity.</param>
/// <param name="AuthorizedVoter">The initial vote authority.</param>
/// <param name="AuthorizedWithdrawer">The withdrawal authority.</param>
/// <param name="Commission">The commission percentage.</param>
public readonly record struct VoteInitialize(
    PublicKey Node,
    PublicKey AuthorizedVoter,
    PublicKey AuthorizedWithdrawer,
    byte Commission);

/// <summary>A slot and its tower confirmation count.</summary>
/// <param name="Slot">The voted slot.</param>
/// <param name="ConfirmationCount">The confirmation count.</param>
public readonly record struct VoteLockout(ulong Slot, uint ConfirmationCount);

/// <summary>A vote authorization selector, including optional BLS credentials.</summary>
public sealed class VoteAuthorization
{
    /// <summary>The compressed BLS public-key length.</summary>
    public const int BlsPublicKeyLength = 48;

    /// <summary>The compressed BLS proof-of-possession length.</summary>
    public const int BlsProofOfPossessionLength = 96;
    private readonly byte[] _blsPublicKey;
    private readonly byte[] _blsProofOfPossession;
    private readonly BlsPublicKey? _validatedPublicKey;
    private readonly BlsProofOfPossession? _validatedProof;

    private VoteAuthorization(
        VoteAuthorizationKind kind,
        ReadOnlySpan<byte> blsPublicKey,
        ReadOnlySpan<byte> blsProofOfPossession)
        : this(kind, blsPublicKey.ToArray(), blsProofOfPossession.ToArray(), null, null)
    {
    }

    private VoteAuthorization(
        VoteAuthorizationKind kind,
        byte[] blsPublicKey,
        byte[] blsProofOfPossession,
        BlsPublicKey? validatedPublicKey,
        BlsProofOfPossession? validatedProof)
    {
        Kind = kind;
        _blsPublicKey = blsPublicKey;
        _blsProofOfPossession = blsProofOfPossession;
        _validatedPublicKey = validatedPublicKey;
        _validatedProof = validatedProof;
    }

    /// <summary>Selects the ordinary vote authority.</summary>
    public static VoteAuthorization Voter { get; } = new(VoteAuthorizationKind.Voter, [], []);

    /// <summary>Selects the withdrawal authority.</summary>
    public static VoteAuthorization Withdrawer { get; } = new(VoteAuthorizationKind.Withdrawer, [], []);

    /// <summary>The selected authorization variant.</summary>
    public VoteAuthorizationKind Kind { get; }

    /// <summary>
    /// A defensive copy of the compressed BLS public key for <see cref="VoteAuthorizationKind.VoterWithBls"/>.
    /// </summary>
    public ReadOnlyMemory<byte> BlsPublicKey => new([.. _blsPublicKey]);

    /// <summary>
    /// A defensive copy of the BLS proof of possession for <see cref="VoteAuthorizationKind.VoterWithBls"/>.
    /// </summary>
    public ReadOnlyMemory<byte> BlsProofOfPossession => new([.. _blsProofOfPossession]);

    internal ReadOnlySpan<byte> BlsPublicKeySpan => _blsPublicKey;

    internal ReadOnlySpan<byte> BlsProofOfPossessionSpan => _blsProofOfPossession;

    /// <summary>
    /// Creates a low-level BLS-backed voter authorization variant. This overload validates lengths
    /// only and deliberately performs no native point or proof verification.
    /// </summary>
    /// <param name="blsPublicKey">A 48-byte compressed BLS public key.</param>
    /// <param name="blsProofOfPossession">A 96-byte compressed BLS proof of possession.</param>
    /// <returns>The BLS-backed authorization selector.</returns>
    /// <exception cref="ArgumentException">Either input has the wrong length.</exception>
    public static VoteAuthorization VoterWithBls(
        ReadOnlySpan<byte> blsPublicKey,
        ReadOnlySpan<byte> blsProofOfPossession)
    {
        ValidateLength(blsPublicKey, BlsPublicKeyLength, nameof(blsPublicKey));
        ValidateLength(blsProofOfPossession, BlsProofOfPossessionLength, nameof(blsProofOfPossession));
        return new VoteAuthorization(VoteAuthorizationKind.VoterWithBls, blsPublicKey, blsProofOfPossession);
    }

    /// <summary>
    /// Creates the BLS-backed voter authorization variant from validated BLS points. Vote instruction
    /// builders verify that the proof matches both this key and the actual vote account before serialization.
    /// </summary>
    /// <param name="blsPublicKey">A canonical, subgroup-checked compressed BLS public key.</param>
    /// <param name="blsProofOfPossession">A canonical, subgroup-checked compressed proof of possession.</param>
    /// <returns>The BLS-backed authorization selector.</returns>
    public static VoteAuthorization VoterWithBls(
        BlsPublicKey blsPublicKey,
        BlsProofOfPossession blsProofOfPossession)
    {
        ArgumentNullException.ThrowIfNull(blsPublicKey);
        ArgumentNullException.ThrowIfNull(blsProofOfPossession);
        return new VoteAuthorization(
            VoteAuthorizationKind.VoterWithBls,
            blsPublicKey.ToBytes(),
            blsProofOfPossession.ToBytes(),
            blsPublicKey,
            blsProofOfPossession);
    }

    internal void ValidateProofForVoteAccount(PublicKey voteAccount)
    {
        if (_validatedPublicKey is null || _validatedProof is null)
            return;

        if (!_validatedPublicKey.VerifyVoteProofOfPossession(_validatedProof, voteAccount))
        {
            throw new ArgumentException(
                "The typed BLS proof of possession does not match the BLS public key and vote account.",
                nameof(voteAccount));
        }
    }

    private static void ValidateLength(ReadOnlySpan<byte> value, int expected, string parameterName)
    {
        if (value.Length != expected)
            throw new ArgumentException($"Value must be exactly {expected} bytes, got {value.Length}.", parameterName);
    }
}

/// <summary>Initialization values for vote state V4, including BLS credentials and basis-point commissions.</summary>
public sealed class VoteInitializeV2
{
    private readonly byte[] _blsPublicKey;
    private readonly byte[] _blsProofOfPossession;
    private readonly BlsPublicKey? _validatedPublicKey;
    private readonly BlsProofOfPossession? _validatedProof;

    /// <summary>
    /// Creates low-level V4 vote-account initialization values. This overload validates lengths only
    /// and deliberately performs no native point or proof verification.
    /// </summary>
    /// <param name="node">The validator identity.</param>
    /// <param name="authorizedVoter">The initial vote authority.</param>
    /// <param name="blsPublicKey">A 48-byte compressed BLS public key.</param>
    /// <param name="blsProofOfPossession">A 96-byte compressed BLS proof of possession.</param>
    /// <param name="authorizedWithdrawer">The withdrawal authority.</param>
    /// <param name="inflationRewardsCommissionBps">Inflation commission in basis points.</param>
    /// <param name="blockRevenueCommissionBps">Block-revenue commission in basis points.</param>
    public VoteInitializeV2(
        PublicKey node,
        PublicKey authorizedVoter,
        ReadOnlySpan<byte> blsPublicKey,
        ReadOnlySpan<byte> blsProofOfPossession,
        PublicKey authorizedWithdrawer,
        ushort inflationRewardsCommissionBps,
        ushort blockRevenueCommissionBps)
        : this(
            node,
            authorizedVoter,
            CopyAndValidate(blsPublicKey, VoteAuthorization.BlsPublicKeyLength, nameof(blsPublicKey)),
            CopyAndValidate(
                blsProofOfPossession,
                VoteAuthorization.BlsProofOfPossessionLength,
                nameof(blsProofOfPossession)),
            authorizedWithdrawer,
            inflationRewardsCommissionBps,
            blockRevenueCommissionBps,
            null,
            null)
    {
    }

    /// <summary>
    /// Creates V4 initialization values from validated BLS points. Vote instruction builders verify
    /// that the proof matches both this key and the actual vote account before serialization.
    /// </summary>
    /// <param name="node">The validator identity.</param>
    /// <param name="authorizedVoter">The initial vote authority.</param>
    /// <param name="blsPublicKey">A canonical, subgroup-checked compressed BLS public key.</param>
    /// <param name="blsProofOfPossession">A canonical, subgroup-checked compressed proof of possession.</param>
    /// <param name="authorizedWithdrawer">The withdrawal authority.</param>
    /// <param name="inflationRewardsCommissionBps">Inflation commission in basis points.</param>
    /// <param name="blockRevenueCommissionBps">Block-revenue commission in basis points.</param>
    public VoteInitializeV2(
        PublicKey node,
        PublicKey authorizedVoter,
        BlsPublicKey blsPublicKey,
        BlsProofOfPossession blsProofOfPossession,
        PublicKey authorizedWithdrawer,
        ushort inflationRewardsCommissionBps,
        ushort blockRevenueCommissionBps)
        : this(
            node,
            authorizedVoter,
            CopyPublicKey(blsPublicKey),
            CopyProof(blsProofOfPossession),
            authorizedWithdrawer,
            inflationRewardsCommissionBps,
            blockRevenueCommissionBps,
            RequirePublicKey(blsPublicKey),
            RequireProof(blsProofOfPossession))
    {
    }

    private VoteInitializeV2(
        PublicKey node,
        PublicKey authorizedVoter,
        byte[] blsPublicKey,
        byte[] blsProofOfPossession,
        PublicKey authorizedWithdrawer,
        ushort inflationRewardsCommissionBps,
        ushort blockRevenueCommissionBps,
        BlsPublicKey? validatedPublicKey,
        BlsProofOfPossession? validatedProof)
    {
        Node = node;
        AuthorizedVoter = authorizedVoter;
        _blsPublicKey = blsPublicKey;
        _blsProofOfPossession = blsProofOfPossession;
        AuthorizedWithdrawer = authorizedWithdrawer;
        InflationRewardsCommissionBps = inflationRewardsCommissionBps;
        BlockRevenueCommissionBps = blockRevenueCommissionBps;
        _validatedPublicKey = validatedPublicKey;
        _validatedProof = validatedProof;
    }

    /// <summary>The validator identity.</summary>
    public PublicKey Node { get; }

    /// <summary>The initial vote authority.</summary>
    public PublicKey AuthorizedVoter { get; }

    /// <summary>A defensive copy of the compressed BLS public key.</summary>
    public ReadOnlyMemory<byte> BlsPublicKey => new([.. _blsPublicKey]);

    /// <summary>A defensive copy of the compressed BLS proof of possession.</summary>
    public ReadOnlyMemory<byte> BlsProofOfPossession => new([.. _blsProofOfPossession]);

    /// <summary>The withdrawal authority.</summary>
    public PublicKey AuthorizedWithdrawer { get; }

    /// <summary>The inflation commission in basis points.</summary>
    public ushort InflationRewardsCommissionBps { get; }

    /// <summary>The block-revenue commission in basis points.</summary>
    public ushort BlockRevenueCommissionBps { get; }

    internal ReadOnlySpan<byte> BlsPublicKeySpan => _blsPublicKey;

    internal ReadOnlySpan<byte> BlsProofOfPossessionSpan => _blsProofOfPossession;

    internal void ValidateProofForVoteAccount(PublicKey voteAccount)
    {
        if (_validatedPublicKey is null || _validatedProof is null)
            return;

        if (!_validatedPublicKey.VerifyVoteProofOfPossession(_validatedProof, voteAccount))
        {
            throw new ArgumentException(
                "The typed BLS proof of possession does not match the BLS public key and vote account.",
                nameof(voteAccount));
        }
    }

    private static byte[] CopyAndValidate(ReadOnlySpan<byte> value, int expectedLength, string parameterName)
    {
        if (value.Length != expectedLength)
            throw new ArgumentException($"Value must be exactly {expectedLength} bytes.", parameterName);

        return value.ToArray();
    }

    private static byte[] CopyPublicKey(BlsPublicKey publicKey) =>
        RequirePublicKey(publicKey).ToBytes();

    private static byte[] CopyProof(BlsProofOfPossession proof) =>
        RequireProof(proof).ToBytes();

    private static BlsPublicKey RequirePublicKey(BlsPublicKey publicKey) =>
        publicKey ?? throw new ArgumentNullException(nameof(publicKey));

    private static BlsProofOfPossession RequireProof(BlsProofOfPossession proof) =>
        proof ?? throw new ArgumentNullException(nameof(proof));
}

/// <summary>A legacy vote payload containing recent slots.</summary>
/// <param name="Slots">Slots ordered from oldest to newest.</param>
/// <param name="Hash">The bank hash for the last slot.</param>
/// <param name="Timestamp">An optional processing timestamp.</param>
public sealed record VoteData(IReadOnlyList<ulong> Slots, Hash Hash, long? Timestamp = null);

/// <summary>A proposed on-chain vote-state update.</summary>
/// <param name="Lockouts">Tower lockouts ordered from oldest to newest.</param>
/// <param name="Root">The proposed root slot.</param>
/// <param name="Hash">The bank hash for the last slot.</param>
/// <param name="Timestamp">An optional processing timestamp.</param>
public sealed record VoteStateUpdate(
    IReadOnlyList<VoteLockout> Lockouts,
    ulong? Root,
    Hash Hash,
    long? Timestamp = null);

/// <summary>A compact tower synchronization payload.</summary>
/// <param name="Lockouts">Tower lockouts ordered from oldest to newest.</param>
/// <param name="Root">The proposed root slot.</param>
/// <param name="Hash">The bank hash for the last slot.</param>
/// <param name="BlockId">The unique identifier of the chain through the block.</param>
/// <param name="Timestamp">An optional processing timestamp.</param>
public sealed record VoteTowerSync(
    IReadOnlyList<VoteLockout> Lockouts,
    ulong? Root,
    Hash Hash,
    Hash BlockId,
    long? Timestamp = null);
