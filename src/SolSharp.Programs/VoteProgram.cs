using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds bincode-compatible instructions for Solana's native Vote program.</summary>
public static class VoteProgram
{
    /// <summary>The account size used by the current V4 vote state.</summary>
    public const int AccountDataLength = 3762;

    /// <summary>The native Vote program address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("Vote111111111111111111111111111111111111111");

    private const uint InitializeAccountDiscriminator = 0;
    private const uint AuthorizeDiscriminator = 1;
    private const uint VoteDiscriminator = 2;
    private const uint WithdrawDiscriminator = 3;
    private const uint UpdateValidatorIdentityDiscriminator = 4;
    private const uint UpdateCommissionDiscriminator = 5;
    private const uint VoteSwitchDiscriminator = 6;
    private const uint AuthorizeCheckedDiscriminator = 7;
    private const uint UpdateVoteStateDiscriminator = 8;
    private const uint UpdateVoteStateSwitchDiscriminator = 9;
    private const uint AuthorizeWithSeedDiscriminator = 10;
    private const uint AuthorizeCheckedWithSeedDiscriminator = 11;
    private const uint CompactUpdateVoteStateDiscriminator = 12;
    private const uint CompactUpdateVoteStateSwitchDiscriminator = 13;
    private const uint TowerSyncDiscriminator = 14;
    private const uint TowerSyncSwitchDiscriminator = 15;
    private const uint InitializeAccountV2Discriminator = 16;
    private const uint UpdateCommissionCollectorDiscriminator = 17;
    private const uint UpdateCommissionBpsDiscriminator = 18;
    private const uint DepositDelegatorRewardsDiscriminator = 19;

    private static readonly PublicKey ClockSysvar = PublicKey.Parse(Sysvars.Clock);
    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);
    private static readonly PublicKey SlotHashesSysvar =
        PublicKey.Parse("SysvarS1otHashes111111111111111111111111111");

    /// <summary>Initializes an allocated legacy vote account.</summary>
    /// <param name="voteAccount">The uninitialized writable vote account.</param>
    /// <param name="initialize">The initialization values.</param>
    /// <returns>The initialize instruction.</returns>
    public static Instruction InitializeAccount(PublicKey voteAccount, VoteInitialize initialize)
    {
        var data = ProgramWireEncoding.Build(InitializeAccountDiscriminator, stream =>
        {
            ProgramWireEncoding.WritePublicKey(stream, initialize.Node);
            ProgramWireEncoding.WritePublicKey(stream, initialize.AuthorizedVoter);
            ProgramWireEncoding.WritePublicKey(stream, initialize.AuthorizedWithdrawer);
            ProgramWireEncoding.WriteByte(stream, initialize.Commission);
        });
        return CreateInstruction(
            data,
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.Readonly(RentSysvar),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.ReadonlySigner(initialize.Node)
            ]);
    }

    /// <summary>Initializes an allocated V4 vote account with BLS credentials and collector accounts.</summary>
    /// <param name="voteAccount">The uninitialized writable vote account.</param>
    /// <param name="initialize">The V4 initialization values.</param>
    /// <param name="inflationRewardsCollector">The writable inflation-rewards collector.</param>
    /// <param name="blockRevenueCollector">The writable block-revenue collector.</param>
    /// <returns>The V2 initialize instruction.</returns>
    public static Instruction InitializeAccountV2(
        PublicKey voteAccount,
        VoteInitializeV2 initialize,
        PublicKey inflationRewardsCollector,
        PublicKey blockRevenueCollector)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        initialize.ValidateProofForVoteAccount(voteAccount);
        var data = ProgramWireEncoding.Build(InitializeAccountV2Discriminator, stream =>
        {
            ProgramWireEncoding.WritePublicKey(stream, initialize.Node);
            ProgramWireEncoding.WritePublicKey(stream, initialize.AuthorizedVoter);
            stream.Write(initialize.BlsPublicKeySpan);
            stream.Write(initialize.BlsProofOfPossessionSpan);
            ProgramWireEncoding.WritePublicKey(stream, initialize.AuthorizedWithdrawer);
            ProgramWireEncoding.WriteUInt16(stream, initialize.InflationRewardsCommissionBps);
            ProgramWireEncoding.WriteUInt16(stream, initialize.BlockRevenueCommissionBps);
        });
        return CreateInstruction(
            data,
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.ReadonlySigner(initialize.Node),
                AccountMeta.Writable(inflationRewardsCollector),
                AccountMeta.Writable(blockRevenueCollector)
            ]);
    }

    /// <summary>Creates and initializes a legacy vote account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="voteAccount">The new vote-account signer.</param>
    /// <param name="initialize">The initialization values.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="space">The allocated size; defaults to current V4 capacity.</param>
    /// <returns>The System create instruction followed by Vote initialize.</returns>
    public static Instruction[] CreateAccount(
        PublicKey payer,
        PublicKey voteAccount,
        VoteInitialize initialize,
        ulong lamports,
        ulong space = AccountDataLength)
        =>
        [
            SystemProgram.CreateAccount(payer, voteAccount, lamports, space, ProgramId),
            InitializeAccount(voteAccount, initialize)
        ];

    /// <summary>Creates and initializes a legacy vote account derived with a System-program seed.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="voteAccount">The derived vote-account address.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program seed.</param>
    /// <param name="initialize">The initialization values.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="space">The allocated size; defaults to current V4 capacity.</param>
    /// <returns>The create-with-seed instruction followed by Vote initialize.</returns>
    public static Instruction[] CreateAccountWithSeed(
        PublicKey payer,
        PublicKey voteAccount,
        PublicKey baseAccount,
        string seed,
        VoteInitialize initialize,
        ulong lamports,
        ulong space = AccountDataLength)
        =>
        [
            SystemProgram.CreateAccountWithSeed(
                payer,
                voteAccount,
                baseAccount,
                seed,
                lamports,
                space,
                ProgramId),
            InitializeAccount(voteAccount, initialize)
        ];

    /// <summary>Creates and V2-initializes a current V4 vote account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="voteAccount">The new vote-account signer.</param>
    /// <param name="initialize">The V4 initialization values.</param>
    /// <param name="inflationRewardsCollector">The writable inflation-rewards collector.</param>
    /// <param name="blockRevenueCollector">The writable block-revenue collector.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="space">The allocated size; defaults to current V4 capacity.</param>
    /// <returns>The System create instruction followed by V2 initialization.</returns>
    public static Instruction[] CreateAccountV2(
        PublicKey payer,
        PublicKey voteAccount,
        VoteInitializeV2 initialize,
        PublicKey inflationRewardsCollector,
        PublicKey blockRevenueCollector,
        ulong lamports,
        ulong space = AccountDataLength)
        =>
        [
            SystemProgram.CreateAccount(payer, voteAccount, lamports, space, ProgramId),
            InitializeAccountV2(voteAccount, initialize, inflationRewardsCollector, blockRevenueCollector)
        ];

    /// <summary>Creates and V2-initializes a derived current vote account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="voteAccount">The derived vote-account address.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program seed.</param>
    /// <param name="initialize">The V4 initialization values.</param>
    /// <param name="inflationRewardsCollector">The writable inflation-rewards collector.</param>
    /// <param name="blockRevenueCollector">The writable block-revenue collector.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="space">The allocated size; defaults to current V4 capacity.</param>
    /// <returns>The create-with-seed instruction followed by V2 initialization.</returns>
    public static Instruction[] CreateAccountV2WithSeed(
        PublicKey payer,
        PublicKey voteAccount,
        PublicKey baseAccount,
        string seed,
        VoteInitializeV2 initialize,
        PublicKey inflationRewardsCollector,
        PublicKey blockRevenueCollector,
        ulong lamports,
        ulong space = AccountDataLength)
        =>
        [
            SystemProgram.CreateAccountWithSeed(
                payer,
                voteAccount,
                baseAccount,
                seed,
                lamports,
                space,
                ProgramId),
            InitializeAccountV2(voteAccount, initialize, inflationRewardsCollector, blockRevenueCollector)
        ];

    /// <summary>Changes a vote-account authority.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority.</param>
    /// <param name="authorization">The role and optional BLS credentials.</param>
    /// <returns>The authorize instruction.</returns>
    public static Instruction Authorize(
        PublicKey voteAccount,
        PublicKey currentAuthority,
        PublicKey newAuthority,
        VoteAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        authorization.ValidateProofForVoteAccount(voteAccount);
        return CreateInstruction(
            ProgramWireEncoding.Build(AuthorizeDiscriminator, stream =>
            {
                ProgramWireEncoding.WritePublicKey(stream, newAuthority);
                WriteAuthorization(stream, authorization);
            }),
            VoteAuthorityAccounts(voteAccount, currentAuthority, null));
    }

    /// <summary>Changes a vote-account authority and requires the replacement key to sign.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <param name="authorization">The role and optional BLS credentials.</param>
    /// <returns>The checked authorize instruction.</returns>
    public static Instruction AuthorizeChecked(
        PublicKey voteAccount,
        PublicKey currentAuthority,
        PublicKey newAuthority,
        VoteAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        authorization.ValidateProofForVoteAccount(voteAccount);
        return CreateInstruction(
            ProgramWireEncoding.Build(
                AuthorizeCheckedDiscriminator,
                stream => WriteAuthorization(stream, authorization)),
            VoteAuthorityAccounts(voteAccount, currentAuthority, newAuthority));
    }

    /// <summary>Changes a vote-account authority through a derived current authority.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="currentAuthorityBase">The derivation base signer.</param>
    /// <param name="currentAuthorityOwner">The derivation owner.</param>
    /// <param name="currentAuthoritySeed">The derivation seed.</param>
    /// <param name="newAuthority">The replacement authority.</param>
    /// <param name="authorization">The role and optional BLS credentials.</param>
    /// <returns>The authorize-with-seed instruction.</returns>
    public static Instruction AuthorizeWithSeed(
        PublicKey voteAccount,
        PublicKey currentAuthorityBase,
        PublicKey currentAuthorityOwner,
        string currentAuthoritySeed,
        PublicKey newAuthority,
        VoteAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        authorization.ValidateProofForVoteAccount(voteAccount);
        var data = ProgramWireEncoding.Build(AuthorizeWithSeedDiscriminator, stream =>
        {
            WriteAuthorization(stream, authorization);
            ProgramWireEncoding.WritePublicKey(stream, currentAuthorityOwner);
            ProgramWireEncoding.WriteString(stream, currentAuthoritySeed, nameof(currentAuthoritySeed));
            ProgramWireEncoding.WritePublicKey(stream, newAuthority);
        });
        return CreateInstruction(data, VoteAuthorityAccounts(voteAccount, currentAuthorityBase, null));
    }

    /// <summary>Changes a derived vote authority and requires the replacement key to sign.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="currentAuthorityBase">The derivation base signer.</param>
    /// <param name="currentAuthorityOwner">The derivation owner.</param>
    /// <param name="currentAuthoritySeed">The derivation seed.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <param name="authorization">The role and optional BLS credentials.</param>
    /// <returns>The checked authorize-with-seed instruction.</returns>
    public static Instruction AuthorizeCheckedWithSeed(
        PublicKey voteAccount,
        PublicKey currentAuthorityBase,
        PublicKey currentAuthorityOwner,
        string currentAuthoritySeed,
        PublicKey newAuthority,
        VoteAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        authorization.ValidateProofForVoteAccount(voteAccount);
        var data = ProgramWireEncoding.Build(AuthorizeCheckedWithSeedDiscriminator, stream =>
        {
            WriteAuthorization(stream, authorization);
            ProgramWireEncoding.WritePublicKey(stream, currentAuthorityOwner);
            ProgramWireEncoding.WriteString(stream, currentAuthoritySeed, nameof(currentAuthoritySeed));
        });
        return CreateInstruction(data, VoteAuthorityAccounts(voteAccount, currentAuthorityBase, newAuthority));
    }

    /// <summary>Updates the validator identity stored in a vote account.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="node">The new validator-identity signer.</param>
    /// <returns>The identity-update instruction.</returns>
    public static Instruction UpdateValidatorIdentity(
        PublicKey voteAccount,
        PublicKey withdrawAuthority,
        PublicKey node)
        => CreateInstruction(
            ProgramWireEncoding.Build(UpdateValidatorIdentityDiscriminator),
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.ReadonlySigner(node),
                AccountMeta.ReadonlySigner(withdrawAuthority)
            ]);

    /// <summary>Updates the legacy commission percentage.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="commission">The commission percentage.</param>
    /// <returns>The commission-update instruction.</returns>
    public static Instruction UpdateCommission(
        PublicKey voteAccount,
        PublicKey withdrawAuthority,
        byte commission)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                UpdateCommissionDiscriminator,
                stream => ProgramWireEncoding.WriteByte(stream, commission)),
            [AccountMeta.Writable(voteAccount), AccountMeta.ReadonlySigner(withdrawAuthority)]);

    /// <summary>Updates one commission collector.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="newCollector">The writable replacement collector.</param>
    /// <param name="kind">The commission stream to update.</param>
    /// <returns>The collector-update instruction.</returns>
    public static Instruction UpdateCommissionCollector(
        PublicKey voteAccount,
        PublicKey withdrawAuthority,
        PublicKey newCollector,
        VoteCommissionKind kind)
    {
        ValidateCommissionKind(kind);
        return CreateInstruction(
            ProgramWireEncoding.Build(
                UpdateCommissionCollectorDiscriminator,
                stream => ProgramWireEncoding.WriteUInt32(stream, (uint)kind)),
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.Writable(newCollector),
                AccountMeta.ReadonlySigner(withdrawAuthority)
            ]);
    }

    /// <summary>Updates one commission rate in basis points.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="kind">The commission stream to update.</param>
    /// <param name="commissionBps">The replacement rate in basis points.</param>
    /// <returns>The basis-point commission instruction.</returns>
    public static Instruction UpdateCommissionBps(
        PublicKey voteAccount,
        PublicKey withdrawAuthority,
        VoteCommissionKind kind,
        ushort commissionBps)
    {
        ValidateCommissionKind(kind);
        return CreateInstruction(
            ProgramWireEncoding.Build(UpdateCommissionBpsDiscriminator, stream =>
            {
                ProgramWireEncoding.WriteUInt16(stream, commissionBps);
                ProgramWireEncoding.WriteUInt32(stream, (uint)kind);
            }),
            [AccountMeta.Writable(voteAccount), AccountMeta.ReadonlySigner(withdrawAuthority)]);
    }

    /// <summary>Deposits lamports for later distribution to stake delegators.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="source">The writable funding signer.</param>
    /// <param name="lamports">The deposit amount.</param>
    /// <returns>The delegator-rewards deposit instruction.</returns>
    public static Instruction DepositDelegatorRewards(
        PublicKey voteAccount,
        PublicKey source,
        ulong lamports)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                DepositDelegatorRewardsDiscriminator,
                stream => ProgramWireEncoding.WriteUInt64(stream, lamports)),
            [AccountMeta.Writable(voteAccount), AccountMeta.WritableSigner(source)]);

    /// <summary>Submits a legacy vote containing recent slots.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="vote">The vote payload.</param>
    /// <returns>The vote instruction.</returns>
    public static Instruction Vote(PublicKey voteAccount, PublicKey voteAuthority, VoteData vote)
        => VoteInternal(VoteDiscriminator, voteAccount, voteAuthority, vote, null);

    /// <summary>Submits a legacy vote with a fork-switch proof hash.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="vote">The vote payload.</param>
    /// <param name="proofHash">The fork-switch proof hash.</param>
    /// <returns>The vote-switch instruction.</returns>
    public static Instruction VoteSwitch(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteData vote,
        Hash proofHash)
        => VoteInternal(VoteSwitchDiscriminator, voteAccount, voteAuthority, vote, proofHash);

    /// <summary>Submits an ordinary bincode vote-state update.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="update">The proposed tower update.</param>
    /// <returns>The update instruction.</returns>
    public static Instruction UpdateVoteState(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteStateUpdate update)
        => VoteStateUpdateInternal(UpdateVoteStateDiscriminator, voteAccount, voteAuthority, update, null, compact: false);

    /// <summary>Submits an ordinary vote-state update with a fork-switch proof.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="update">The proposed tower update.</param>
    /// <param name="proofHash">The fork-switch proof hash.</param>
    /// <returns>The update-switch instruction.</returns>
    public static Instruction UpdateVoteStateSwitch(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteStateUpdate update,
        Hash proofHash)
        => VoteStateUpdateInternal(
            UpdateVoteStateSwitchDiscriminator,
            voteAccount,
            voteAuthority,
            update,
            proofHash,
            compact: false);

    /// <summary>Submits a compact, offset-encoded vote-state update.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="update">The proposed tower update.</param>
    /// <returns>The compact update instruction.</returns>
    public static Instruction CompactUpdateVoteState(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteStateUpdate update)
        => VoteStateUpdateInternal(
            CompactUpdateVoteStateDiscriminator,
            voteAccount,
            voteAuthority,
            update,
            null,
            compact: true);

    /// <summary>Submits a compact vote-state update with a fork-switch proof.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="update">The proposed tower update.</param>
    /// <param name="proofHash">The fork-switch proof hash.</param>
    /// <returns>The compact update-switch instruction.</returns>
    public static Instruction CompactUpdateVoteStateSwitch(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteStateUpdate update,
        Hash proofHash)
        => VoteStateUpdateInternal(
            CompactUpdateVoteStateSwitchDiscriminator,
            voteAccount,
            voteAuthority,
            update,
            proofHash,
            compact: true);

    /// <summary>Synchronizes on-chain vote state with a compact local tower.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="tower">The tower payload.</param>
    /// <returns>The tower-sync instruction.</returns>
    public static Instruction TowerSync(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteTowerSync tower)
        => TowerSyncInternal(TowerSyncDiscriminator, voteAccount, voteAuthority, tower, null);

    /// <summary>Synchronizes on-chain vote state with a compact tower and fork-switch proof.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="voteAuthority">The vote-authority signer.</param>
    /// <param name="tower">The tower payload.</param>
    /// <param name="proofHash">The fork-switch proof hash.</param>
    /// <returns>The tower-sync-switch instruction.</returns>
    public static Instruction TowerSyncSwitch(
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteTowerSync tower,
        Hash proofHash)
        => TowerSyncInternal(TowerSyncSwitchDiscriminator, voteAccount, voteAuthority, tower, proofHash);

    /// <summary>Withdraws lamports from a vote account.</summary>
    /// <param name="voteAccount">The writable vote account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="lamports">The amount to withdraw.</param>
    /// <param name="recipient">The writable recipient.</param>
    /// <returns>The withdraw instruction.</returns>
    public static Instruction Withdraw(
        PublicKey voteAccount,
        PublicKey withdrawAuthority,
        ulong lamports,
        PublicKey recipient)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                WithdrawDiscriminator,
                stream => ProgramWireEncoding.WriteUInt64(stream, lamports)),
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.Writable(recipient),
                AccountMeta.ReadonlySigner(withdrawAuthority)
            ]);

    private static Instruction VoteInternal(
        uint discriminator,
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteData vote,
        Hash? proofHash)
    {
        ArgumentNullException.ThrowIfNull(vote);
        ArgumentNullException.ThrowIfNull(vote.Slots);
        var data = ProgramWireEncoding.Build(discriminator, stream =>
        {
            ProgramWireEncoding.WriteUInt64(stream, checked((ulong)vote.Slots.Count));
            foreach (var slot in vote.Slots)
                ProgramWireEncoding.WriteUInt64(stream, slot);
            ProgramWireEncoding.WriteHash(stream, vote.Hash);
            ProgramWireEncoding.WriteOptionalInt64(stream, vote.Timestamp);
            if (proofHash is { } hash)
                ProgramWireEncoding.WriteHash(stream, hash);
        });
        return CreateInstruction(data, VoteAccounts(voteAccount, voteAuthority));
    }

    private static Instruction VoteStateUpdateInternal(
        uint discriminator,
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteStateUpdate update,
        Hash? proofHash,
        bool compact)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(update.Lockouts);
        var data = ProgramWireEncoding.Build(discriminator, stream =>
        {
            if (compact)
                WriteCompactTower(stream, update.Lockouts, update.Root, update.Hash, update.Timestamp);
            else
                WriteVoteStateUpdate(stream, update);
            if (proofHash is { } hash)
                ProgramWireEncoding.WriteHash(stream, hash);
        });
        return CreateInstruction(data, StateUpdateAccounts(voteAccount, voteAuthority));
    }

    private static Instruction TowerSyncInternal(
        uint discriminator,
        PublicKey voteAccount,
        PublicKey voteAuthority,
        VoteTowerSync tower,
        Hash? proofHash)
    {
        ArgumentNullException.ThrowIfNull(tower);
        ArgumentNullException.ThrowIfNull(tower.Lockouts);
        var data = ProgramWireEncoding.Build(discriminator, stream =>
        {
            WriteCompactTower(stream, tower.Lockouts, tower.Root, tower.Hash, tower.Timestamp);
            ProgramWireEncoding.WriteHash(stream, tower.BlockId);
            if (proofHash is { } hash)
                ProgramWireEncoding.WriteHash(stream, hash);
        });
        return CreateInstruction(data, StateUpdateAccounts(voteAccount, voteAuthority));
    }

    private static void WriteVoteStateUpdate(MemoryStream stream, VoteStateUpdate update)
    {
        ProgramWireEncoding.WriteUInt64(stream, checked((ulong)update.Lockouts.Count));
        foreach (var lockout in update.Lockouts)
        {
            ProgramWireEncoding.WriteUInt64(stream, lockout.Slot);
            ProgramWireEncoding.WriteUInt32(stream, lockout.ConfirmationCount);
        }

        ProgramWireEncoding.WriteOptionalUInt64(stream, update.Root);
        ProgramWireEncoding.WriteHash(stream, update.Hash);
        ProgramWireEncoding.WriteOptionalInt64(stream, update.Timestamp);
    }

    private static void WriteCompactTower(
        MemoryStream stream,
        IReadOnlyList<VoteLockout> lockouts,
        ulong? root,
        Hash hash,
        long? timestamp)
    {
        ProgramWireEncoding.WriteUInt64(stream, root ?? ulong.MaxValue);
        ProgramWireEncoding.WriteShortVectorLength(stream, lockouts.Count);

        var previousSlot = root ?? 0;
        foreach (var lockout in lockouts)
        {
            if (lockout.Slot < previousSlot)
            {
                throw new ArgumentException(
                    "Compact vote lockout slots must be ordered and must not precede the root.",
                    nameof(lockouts));
            }

            if (lockout.ConfirmationCount > byte.MaxValue)
            {
                throw new ArgumentException(
                    "A compact vote confirmation count must fit in one byte.",
                    nameof(lockouts));
            }

            ProgramWireEncoding.WriteUnsignedLeb128(stream, lockout.Slot - previousSlot);
            ProgramWireEncoding.WriteByte(stream, (byte)lockout.ConfirmationCount);
            previousSlot = lockout.Slot;
        }

        ProgramWireEncoding.WriteHash(stream, hash);
        ProgramWireEncoding.WriteOptionalInt64(stream, timestamp);
    }

    private static void WriteAuthorization(MemoryStream stream, VoteAuthorization authorization)
    {
        ProgramWireEncoding.WriteUInt32(stream, (uint)authorization.Kind);
        if (authorization.Kind == VoteAuthorizationKind.VoterWithBls)
        {
            stream.Write(authorization.BlsPublicKeySpan);
            stream.Write(authorization.BlsProofOfPossessionSpan);
        }
    }

    private static IReadOnlyList<AccountMeta> VoteAuthorityAccounts(
        PublicKey voteAccount,
        PublicKey currentAuthority,
        PublicKey? newAuthority)
        => newAuthority is { } replacement
            ?
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.ReadonlySigner(currentAuthority),
                AccountMeta.ReadonlySigner(replacement)
            ]
            :
            [
                AccountMeta.Writable(voteAccount),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.ReadonlySigner(currentAuthority)
            ];

    private static IReadOnlyList<AccountMeta> VoteAccounts(PublicKey voteAccount, PublicKey voteAuthority)
        =>
        [
            AccountMeta.Writable(voteAccount),
            AccountMeta.Readonly(SlotHashesSysvar),
            AccountMeta.Readonly(ClockSysvar),
            AccountMeta.ReadonlySigner(voteAuthority)
        ];

    private static IReadOnlyList<AccountMeta> StateUpdateAccounts(
        PublicKey voteAccount,
        PublicKey voteAuthority)
        => [AccountMeta.Writable(voteAccount), AccountMeta.ReadonlySigner(voteAuthority)];

    private static void ValidateCommissionKind(VoteCommissionKind kind)
    {
        if (kind is not VoteCommissionKind.InflationRewards and not VoteCommissionKind.BlockRevenue)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown vote commission kind.");
    }

    private static Instruction CreateInstruction(byte[] data, IReadOnlyList<AccountMeta> accounts)
        => new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
}
