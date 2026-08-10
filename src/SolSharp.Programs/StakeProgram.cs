using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds bincode-compatible instructions for Solana's native Stake program.</summary>
public static class StakeProgram
{
    /// <summary>The fixed serialized size of a stake account.</summary>
    public const int AccountDataLength = StakeAccountState.AccountDataLength;

    /// <summary>The native Stake program address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("Stake11111111111111111111111111111111111111");

    private const uint InitializeDiscriminator = 0;
    private const uint AuthorizeDiscriminator = 1;
    private const uint DelegateDiscriminator = 2;
    private const uint SplitDiscriminator = 3;
    private const uint WithdrawDiscriminator = 4;
    private const uint DeactivateDiscriminator = 5;
    private const uint SetLockupDiscriminator = 6;
    private const uint MergeDiscriminator = 7;
    private const uint AuthorizeWithSeedDiscriminator = 8;
    private const uint InitializeCheckedDiscriminator = 9;
    private const uint AuthorizeCheckedDiscriminator = 10;
    private const uint AuthorizeCheckedWithSeedDiscriminator = 11;
    private const uint SetLockupCheckedDiscriminator = 12;
    private const uint GetMinimumDelegationDiscriminator = 13;
    private const uint DeactivateDelinquentDiscriminator = 14;
    private const uint MoveStakeDiscriminator = 16;
    private const uint MoveLamportsDiscriminator = 17;

    private static readonly PublicKey ClockSysvar = PublicKey.Parse(Sysvars.Clock);
    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);
    private static readonly PublicKey StakeHistorySysvar =
        PublicKey.Parse("SysvarStakeHistory1111111111111111111111111");

    private static readonly PublicKey StakeConfig =
        PublicKey.Parse("StakeConfig11111111111111111111111111111111");

    /// <summary>Initializes an allocated stake account with authorities and a lockup.</summary>
    /// <param name="stakeAccount">The uninitialized writable stake account.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lockup">The initial withdrawal lockup.</param>
    /// <returns>The initialize instruction.</returns>
    public static Instruction Initialize(
        PublicKey stakeAccount,
        StakeAuthorized authorized,
        StakeLockup lockup)
    {
        var data = ProgramWireEncoding.Build(InitializeDiscriminator, stream =>
        {
            ProgramWireEncoding.WritePublicKey(stream, authorized.Staker);
            ProgramWireEncoding.WritePublicKey(stream, authorized.Withdrawer);
            ProgramWireEncoding.WriteInt64(stream, lockup.UnixTimestamp);
            ProgramWireEncoding.WriteUInt64(stream, lockup.Epoch);
            ProgramWireEncoding.WritePublicKey(stream, lockup.Custodian);
        });

        return CreateInstruction(
            data,
            [AccountMeta.Writable(stakeAccount), AccountMeta.Readonly(RentSysvar)]);
    }

    /// <summary>Initializes a stake account while requiring the withdrawal authority to sign.</summary>
    /// <param name="stakeAccount">The uninitialized writable stake account.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <returns>The checked initialize instruction.</returns>
    public static Instruction InitializeChecked(PublicKey stakeAccount, StakeAuthorized authorized)
        => CreateInstruction(
            ProgramWireEncoding.Build(InitializeCheckedDiscriminator),
            [
                AccountMeta.Writable(stakeAccount),
                AccountMeta.Readonly(RentSysvar),
                AccountMeta.Readonly(authorized.Staker),
                AccountMeta.ReadonlySigner(authorized.Withdrawer)
            ]);

    /// <summary>Creates and initializes a stake account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The new stake-account signer.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lockup">The initial lockup.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The System create instruction followed by Stake initialize.</returns>
    public static Instruction[] CreateAccount(
        PublicKey payer,
        PublicKey stakeAccount,
        StakeAuthorized authorized,
        StakeLockup lockup,
        ulong lamports)
        =>
        [
            SystemProgram.CreateAccount(payer, stakeAccount, lamports, AccountDataLength, ProgramId),
            Initialize(stakeAccount, authorized, lockup)
        ];

    /// <summary>Creates and checked-initializes a stake account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The new stake-account signer.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The System create instruction followed by checked initialization.</returns>
    public static Instruction[] CreateAccountChecked(
        PublicKey payer,
        PublicKey stakeAccount,
        StakeAuthorized authorized,
        ulong lamports)
        =>
        [
            SystemProgram.CreateAccount(payer, stakeAccount, lamports, AccountDataLength, ProgramId),
            InitializeChecked(stakeAccount, authorized)
        ];

    /// <summary>Creates and initializes a stake account derived with a System-program seed.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The derived stake-account address.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program derivation seed.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lockup">The initial lockup.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The System create-with-seed instruction followed by Stake initialize.</returns>
    public static Instruction[] CreateAccountWithSeed(
        PublicKey payer,
        PublicKey stakeAccount,
        PublicKey baseAccount,
        string seed,
        StakeAuthorized authorized,
        StakeLockup lockup,
        ulong lamports)
        =>
        [
            SystemProgram.CreateAccountWithSeed(
                payer,
                stakeAccount,
                baseAccount,
                seed,
                lamports,
                AccountDataLength,
                ProgramId),
            Initialize(stakeAccount, authorized, lockup)
        ];

    /// <summary>Creates and checked-initializes a derived stake account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The derived stake-account address.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program derivation seed.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The System create-with-seed instruction followed by checked initialization.</returns>
    public static Instruction[] CreateAccountWithSeedChecked(
        PublicKey payer,
        PublicKey stakeAccount,
        PublicKey baseAccount,
        string seed,
        StakeAuthorized authorized,
        ulong lamports)
        =>
        [
            SystemProgram.CreateAccountWithSeed(
                payer,
                stakeAccount,
                baseAccount,
                seed,
                lamports,
                AccountDataLength,
                ProgramId),
            InitializeChecked(stakeAccount, authorized)
        ];

    /// <summary>Delegates all stake in an initialized account to a vote account.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="stakeAuthority">The stake-authority signer.</param>
    /// <param name="voteAccount">The vote account receiving the delegation.</param>
    /// <returns>The delegate instruction.</returns>
    public static Instruction DelegateStake(
        PublicKey stakeAccount,
        PublicKey stakeAuthority,
        PublicKey voteAccount)
        => CreateInstruction(
            ProgramWireEncoding.Build(DelegateDiscriminator),
            [
                AccountMeta.Writable(stakeAccount),
                AccountMeta.Readonly(voteAccount),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.Readonly(StakeHistorySysvar),
                AccountMeta.Readonly(StakeConfig),
                AccountMeta.ReadonlySigner(stakeAuthority)
            ]);

    /// <summary>Creates, initializes, and delegates a stake account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The new stake-account signer.</param>
    /// <param name="voteAccount">The vote account receiving the delegation.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lockup">The initial lockup.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The create, initialize, and delegate instructions.</returns>
    public static Instruction[] CreateAccountAndDelegateStake(
        PublicKey payer,
        PublicKey stakeAccount,
        PublicKey voteAccount,
        StakeAuthorized authorized,
        StakeLockup lockup,
        ulong lamports)
    {
        var instructions = CreateAccount(payer, stakeAccount, authorized, lockup, lamports);
        return [.. instructions, DelegateStake(stakeAccount, authorized.Staker, voteAccount)];
    }

    /// <summary>Creates, initializes, and delegates a derived stake account.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="stakeAccount">The derived stake account.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program derivation seed.</param>
    /// <param name="voteAccount">The vote account receiving the delegation.</param>
    /// <param name="authorized">The stake and withdrawal authorities.</param>
    /// <param name="lockup">The initial lockup.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <returns>The create-with-seed, initialize, and delegate instructions.</returns>
    public static Instruction[] CreateAccountWithSeedAndDelegateStake(
        PublicKey payer,
        PublicKey stakeAccount,
        PublicKey baseAccount,
        string seed,
        PublicKey voteAccount,
        StakeAuthorized authorized,
        StakeLockup lockup,
        ulong lamports)
    {
        var instructions = CreateAccountWithSeed(
            payer,
            stakeAccount,
            baseAccount,
            seed,
            authorized,
            lockup,
            lamports);
        return [.. instructions, DelegateStake(stakeAccount, authorized.Staker, voteAccount)];
    }

    /// <summary>Splits lamports and stake into an uninitialized account.</summary>
    /// <param name="stakeAccount">The source stake account.</param>
    /// <param name="stakeAuthority">The stake-authority signer.</param>
    /// <param name="lamports">The amount to split.</param>
    /// <param name="splitStakeAccount">The destination account, which must sign System allocation.</param>
    /// <returns>Allocate, assign, and split instructions.</returns>
    public static Instruction[] SplitStake(
        PublicKey stakeAccount,
        PublicKey stakeAuthority,
        ulong lamports,
        PublicKey splitStakeAccount)
        =>
        [
            SystemProgram.Allocate(splitStakeAccount, AccountDataLength),
            SystemProgram.Assign(splitStakeAccount, ProgramId),
            SplitStakeInstruction(stakeAccount, stakeAuthority, lamports, splitStakeAccount)
        ];

    /// <summary>Splits stake into a destination derived with a System-program seed.</summary>
    /// <param name="stakeAccount">The source stake account.</param>
    /// <param name="stakeAuthority">The stake-authority signer.</param>
    /// <param name="lamports">The amount to split.</param>
    /// <param name="splitStakeAccount">The derived destination account.</param>
    /// <param name="baseAccount">The derivation base signer.</param>
    /// <param name="seed">The System-program derivation seed.</param>
    /// <returns>Allocate-with-seed and split instructions.</returns>
    public static Instruction[] SplitStakeWithSeed(
        PublicKey stakeAccount,
        PublicKey stakeAuthority,
        ulong lamports,
        PublicKey splitStakeAccount,
        PublicKey baseAccount,
        string seed)
        =>
        [
            SystemProgram.AllocateWithSeed(splitStakeAccount, baseAccount, seed, AccountDataLength, ProgramId),
            SplitStakeInstruction(stakeAccount, stakeAuthority, lamports, splitStakeAccount)
        ];

    /// <summary>Builds only the native Stake split instruction for an already allocated destination.</summary>
    /// <param name="stakeAccount">The source stake account.</param>
    /// <param name="stakeAuthority">The stake-authority signer.</param>
    /// <param name="lamports">The amount to split.</param>
    /// <param name="splitStakeAccount">The uninitialized writable destination.</param>
    /// <returns>The split instruction.</returns>
    public static Instruction SplitStakeInstruction(
        PublicKey stakeAccount,
        PublicKey stakeAuthority,
        ulong lamports,
        PublicKey splitStakeAccount)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                SplitDiscriminator,
                stream => ProgramWireEncoding.WriteUInt64(stream, lamports)),
            [
                AccountMeta.Writable(stakeAccount),
                AccountMeta.Writable(splitStakeAccount),
                AccountMeta.ReadonlySigner(stakeAuthority)
            ]);

    /// <summary>Merges a compatible source stake account into a destination.</summary>
    /// <param name="destinationStakeAccount">The writable destination.</param>
    /// <param name="sourceStakeAccount">The writable source that will be drained.</param>
    /// <param name="stakeAuthority">The shared stake-authority signer.</param>
    /// <returns>The merge instruction.</returns>
    public static Instruction Merge(
        PublicKey destinationStakeAccount,
        PublicKey sourceStakeAccount,
        PublicKey stakeAuthority)
        => CreateInstruction(
            ProgramWireEncoding.Build(MergeDiscriminator),
            [
                AccountMeta.Writable(destinationStakeAccount),
                AccountMeta.Writable(sourceStakeAccount),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.Readonly(StakeHistorySysvar),
                AccountMeta.ReadonlySigner(stakeAuthority)
            ]);

    /// <summary>Changes a stake or withdrawal authority.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority.</param>
    /// <param name="authorityType">The authority role to replace.</param>
    /// <param name="custodian">An optional lockup-custodian signer.</param>
    /// <returns>The authorize instruction.</returns>
    public static Instruction Authorize(
        PublicKey stakeAccount,
        PublicKey currentAuthority,
        PublicKey newAuthority,
        StakeAuthorityType authorityType,
        PublicKey? custodian = null)
    {
        ValidateAuthorityType(authorityType);
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(stakeAccount),
            AccountMeta.Readonly(ClockSysvar),
            AccountMeta.ReadonlySigner(currentAuthority)
        };
        AddOptionalSigner(accounts, custodian);

        return CreateInstruction(
            ProgramWireEncoding.Build(AuthorizeDiscriminator, stream =>
            {
                ProgramWireEncoding.WritePublicKey(stream, newAuthority);
                ProgramWireEncoding.WriteUInt32(stream, (uint)authorityType);
            }),
            accounts);
    }

    /// <summary>Changes an authority and requires the replacement key to sign.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <param name="authorityType">The authority role to replace.</param>
    /// <param name="custodian">An optional lockup-custodian signer.</param>
    /// <returns>The checked authorize instruction.</returns>
    public static Instruction AuthorizeChecked(
        PublicKey stakeAccount,
        PublicKey currentAuthority,
        PublicKey newAuthority,
        StakeAuthorityType authorityType,
        PublicKey? custodian = null)
    {
        ValidateAuthorityType(authorityType);
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(stakeAccount),
            AccountMeta.Readonly(ClockSysvar),
            AccountMeta.ReadonlySigner(currentAuthority),
            AccountMeta.ReadonlySigner(newAuthority)
        };
        AddOptionalSigner(accounts, custodian);

        return CreateInstruction(
            ProgramWireEncoding.Build(
                AuthorizeCheckedDiscriminator,
                stream => ProgramWireEncoding.WriteUInt32(stream, (uint)authorityType)),
            accounts);
    }

    /// <summary>Changes an authority using a signer derived from a base key and seed.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="authorityBase">The current authority's base signer.</param>
    /// <param name="authoritySeed">The current authority's seed.</param>
    /// <param name="authorityOwner">The program owner used in the derivation.</param>
    /// <param name="newAuthority">The replacement authority.</param>
    /// <param name="authorityType">The authority role to replace.</param>
    /// <param name="custodian">An optional lockup-custodian signer.</param>
    /// <returns>The authorize-with-seed instruction.</returns>
    public static Instruction AuthorizeWithSeed(
        PublicKey stakeAccount,
        PublicKey authorityBase,
        string authoritySeed,
        PublicKey authorityOwner,
        PublicKey newAuthority,
        StakeAuthorityType authorityType,
        PublicKey? custodian = null)
    {
        ValidateAuthorityType(authorityType);
        var accounts = AuthorityWithSeedAccounts(stakeAccount, authorityBase, null, custodian);
        var data = ProgramWireEncoding.Build(AuthorizeWithSeedDiscriminator, stream =>
        {
            ProgramWireEncoding.WritePublicKey(stream, newAuthority);
            ProgramWireEncoding.WriteUInt32(stream, (uint)authorityType);
            ProgramWireEncoding.WriteString(stream, authoritySeed, nameof(authoritySeed));
            ProgramWireEncoding.WritePublicKey(stream, authorityOwner);
        });
        return CreateInstruction(data, accounts);
    }

    /// <summary>Changes a derived authority and requires the replacement key to sign.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="authorityBase">The current authority's base signer.</param>
    /// <param name="authoritySeed">The current authority's seed.</param>
    /// <param name="authorityOwner">The program owner used in the derivation.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <param name="authorityType">The authority role to replace.</param>
    /// <param name="custodian">An optional lockup-custodian signer.</param>
    /// <returns>The checked authorize-with-seed instruction.</returns>
    public static Instruction AuthorizeCheckedWithSeed(
        PublicKey stakeAccount,
        PublicKey authorityBase,
        string authoritySeed,
        PublicKey authorityOwner,
        PublicKey newAuthority,
        StakeAuthorityType authorityType,
        PublicKey? custodian = null)
    {
        ValidateAuthorityType(authorityType);
        var accounts = AuthorityWithSeedAccounts(stakeAccount, authorityBase, newAuthority, custodian);
        var data = ProgramWireEncoding.Build(AuthorizeCheckedWithSeedDiscriminator, stream =>
        {
            ProgramWireEncoding.WriteUInt32(stream, (uint)authorityType);
            ProgramWireEncoding.WriteString(stream, authoritySeed, nameof(authoritySeed));
            ProgramWireEncoding.WritePublicKey(stream, authorityOwner);
        });
        return CreateInstruction(data, accounts);
    }

    /// <summary>Withdraws unstaked lamports from a stake account.</summary>
    /// <param name="stakeAccount">The writable stake account.</param>
    /// <param name="withdrawAuthority">The withdrawal-authority signer.</param>
    /// <param name="recipient">The writable recipient.</param>
    /// <param name="lamports">The amount to withdraw.</param>
    /// <param name="custodian">An optional lockup-custodian signer.</param>
    /// <returns>The withdraw instruction.</returns>
    public static Instruction Withdraw(
        PublicKey stakeAccount,
        PublicKey withdrawAuthority,
        PublicKey recipient,
        ulong lamports,
        PublicKey? custodian = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(stakeAccount),
            AccountMeta.Writable(recipient),
            AccountMeta.Readonly(ClockSysvar),
            AccountMeta.Readonly(StakeHistorySysvar),
            AccountMeta.ReadonlySigner(withdrawAuthority)
        };
        AddOptionalSigner(accounts, custodian);
        return CreateInstruction(
            ProgramWireEncoding.Build(
                WithdrawDiscriminator,
                stream => ProgramWireEncoding.WriteUInt64(stream, lamports)),
            accounts);
    }

    /// <summary>Deactivates delegated stake.</summary>
    /// <param name="stakeAccount">The writable delegated stake account.</param>
    /// <param name="stakeAuthority">The stake-authority signer.</param>
    /// <returns>The deactivate instruction.</returns>
    public static Instruction Deactivate(PublicKey stakeAccount, PublicKey stakeAuthority)
        => CreateInstruction(
            ProgramWireEncoding.Build(DeactivateDiscriminator),
            [
                AccountMeta.Writable(stakeAccount),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.ReadonlySigner(stakeAuthority)
            ]);

    /// <summary>Updates selected lockup values.</summary>
    /// <param name="stakeAccount">The writable initialized stake account.</param>
    /// <param name="arguments">The optional replacement values.</param>
    /// <param name="authority">The lockup or withdrawal-authority signer.</param>
    /// <returns>The set-lockup instruction.</returns>
    public static Instruction SetLockup(
        PublicKey stakeAccount,
        StakeLockupArguments arguments,
        PublicKey authority)
        => CreateInstruction(
            EncodeLockup(SetLockupDiscriminator, arguments, includeCustodian: true),
            [AccountMeta.Writable(stakeAccount), AccountMeta.ReadonlySigner(authority)]);

    /// <summary>Updates lockup values and requires a replacement custodian to sign.</summary>
    /// <param name="stakeAccount">The writable initialized stake account.</param>
    /// <param name="arguments">The optional replacement values.</param>
    /// <param name="authority">The current lockup or withdrawal-authority signer.</param>
    /// <returns>The checked set-lockup instruction.</returns>
    public static Instruction SetLockupChecked(
        PublicKey stakeAccount,
        StakeLockupArguments arguments,
        PublicKey authority)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(stakeAccount),
            AccountMeta.ReadonlySigner(authority)
        };
        AddOptionalSigner(accounts, arguments.Custodian);
        return CreateInstruction(
            EncodeLockup(SetLockupCheckedDiscriminator, arguments, includeCustodian: false),
            accounts);
    }

    /// <summary>Requests the runtime's current minimum stake delegation as return data.</summary>
    /// <returns>The account-free query instruction.</returns>
    public static Instruction GetMinimumDelegation()
        => CreateInstruction(ProgramWireEncoding.Build(GetMinimumDelegationDiscriminator), []);

    /// <summary>Deactivates stake delegated to a sufficiently delinquent vote account.</summary>
    /// <param name="stakeAccount">The writable delegated stake account.</param>
    /// <param name="delinquentVoteAccount">The delinquent vote account.</param>
    /// <param name="referenceVoteAccount">A recently voting reference account.</param>
    /// <returns>The permissionless delinquent-deactivation instruction.</returns>
    public static Instruction DeactivateDelinquent(
        PublicKey stakeAccount,
        PublicKey delinquentVoteAccount,
        PublicKey referenceVoteAccount)
        => CreateInstruction(
            ProgramWireEncoding.Build(DeactivateDelinquentDiscriminator),
            [
                AccountMeta.Writable(stakeAccount),
                AccountMeta.Readonly(delinquentVoteAccount),
                AccountMeta.Readonly(referenceVoteAccount)
            ]);

    /// <summary>Moves active stake between compatible stake accounts.</summary>
    /// <param name="sourceStakeAccount">The writable source stake account.</param>
    /// <param name="destinationStakeAccount">The writable destination stake account.</param>
    /// <param name="stakeAuthority">The shared stake-authority signer.</param>
    /// <param name="lamports">The active stake to move.</param>
    /// <returns>The move-stake instruction.</returns>
    public static Instruction MoveStake(
        PublicKey sourceStakeAccount,
        PublicKey destinationStakeAccount,
        PublicKey stakeAuthority,
        ulong lamports)
        => Move(
            MoveStakeDiscriminator,
            sourceStakeAccount,
            destinationStakeAccount,
            stakeAuthority,
            lamports);

    /// <summary>Moves unstaked lamports between compatible stake accounts.</summary>
    /// <param name="sourceStakeAccount">The writable source stake account.</param>
    /// <param name="destinationStakeAccount">The writable destination stake account.</param>
    /// <param name="stakeAuthority">The shared stake-authority signer.</param>
    /// <param name="lamports">The unstaked lamports to move.</param>
    /// <returns>The move-lamports instruction.</returns>
    public static Instruction MoveLamports(
        PublicKey sourceStakeAccount,
        PublicKey destinationStakeAccount,
        PublicKey stakeAuthority,
        ulong lamports)
        => Move(
            MoveLamportsDiscriminator,
            sourceStakeAccount,
            destinationStakeAccount,
            stakeAuthority,
            lamports);

    private static Instruction Move(
        uint discriminator,
        PublicKey source,
        PublicKey destination,
        PublicKey authority,
        ulong lamports)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                discriminator,
                stream => ProgramWireEncoding.WriteUInt64(stream, lamports)),
            [
                AccountMeta.Writable(source),
                AccountMeta.Writable(destination),
                AccountMeta.ReadonlySigner(authority)
            ]);

    private static byte[] EncodeLockup(
        uint discriminator,
        StakeLockupArguments arguments,
        bool includeCustodian)
        => ProgramWireEncoding.Build(discriminator, stream =>
        {
            ProgramWireEncoding.WriteOptionalInt64(stream, arguments.UnixTimestamp);
            ProgramWireEncoding.WriteOptionalUInt64(stream, arguments.Epoch);
            if (includeCustodian)
                ProgramWireEncoding.WriteOptionalPublicKey(stream, arguments.Custodian);
        });

    private static List<AccountMeta> AuthorityWithSeedAccounts(
        PublicKey stakeAccount,
        PublicKey authorityBase,
        PublicKey? newAuthority,
        PublicKey? custodian)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(stakeAccount),
            AccountMeta.ReadonlySigner(authorityBase),
            AccountMeta.Readonly(ClockSysvar)
        };
        AddOptionalSigner(accounts, newAuthority);
        AddOptionalSigner(accounts, custodian);
        return accounts;
    }

    private static void AddOptionalSigner(List<AccountMeta> accounts, PublicKey? signer)
    {
        if (signer is { } publicKey)
            accounts.Add(AccountMeta.ReadonlySigner(publicKey));
    }

    private static void ValidateAuthorityType(StakeAuthorityType authorityType)
    {
        if (authorityType is not StakeAuthorityType.Staker and not StakeAuthorityType.Withdrawer)
            throw new ArgumentOutOfRangeException(nameof(authorityType), authorityType, "Unknown stake authority role.");
    }

    private static Instruction CreateInstruction(byte[] data, IReadOnlyList<AccountMeta> accounts)
        => new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
}
