using System.Buffers.Binary;
using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class TokenProgram
{
    private const byte InitializeMultisigDiscriminator = 2;
    private const byte InitializeAccount2Discriminator = 16;
    private const byte InitializeAccount3Discriminator = 18;
    private const byte InitializeMultisig2Discriminator = 19;
    private const byte InitializeMint2Discriminator = 20;
    private const byte GetAccountDataSizeDiscriminator = 21;
    private const byte InitializeImmutableOwnerDiscriminator = 22;
    private const byte AmountToUiAmountDiscriminator = 23;
    private const byte UiAmountToAmountDiscriminator = 24;
    private const byte WithdrawExcessLamportsDiscriminator = 38;
    private const byte UnwrapLamportsDiscriminator = 45;
    private const byte BatchDiscriminator = 255;

    private static readonly UTF8Encoding StrictTokenUtf8 = new(false, true);

    /// <summary>Builds the rent-sysvar-free form of the SPL Token mint initialization instruction.</summary>
    /// <param name="mint">The uninitialized account to initialize as a mint.</param>
    /// <param name="decimals">The number of base-unit decimal places.</param>
    /// <param name="mintAuthority">The authority allowed to mint tokens.</param>
    /// <param name="freezeAuthority">The authority allowed to freeze accounts, or <c>null</c> for none.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeMint2 instruction.</returns>
    public static Instruction InitializeMint2(
        PublicKey mint,
        byte decimals,
        PublicKey mintAuthority,
        PublicKey? freezeAuthority = null,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(mint)],
            Data = InitializeMintData(InitializeMint2Discriminator, decimals, mintAuthority, freezeAuthority)
        };

    /// <summary>Builds InitializeAccount2, which stores the owner in instruction data and still supplies Rent.</summary>
    /// <param name="account">The uninitialized token account.</param>
    /// <param name="mint">The mint the account will hold.</param>
    /// <param name="owner">The account owner encoded in instruction data.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeAccount2 instruction.</returns>
    public static Instruction InitializeAccount2(
        PublicKey account,
        PublicKey mint,
        PublicKey owner,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Readonly(mint), AccountMeta.Readonly(RentSysvar)],
            Data = PublicKeyData(InitializeAccount2Discriminator, owner)
        };

    /// <summary>Builds InitializeAccount3, with the owner in data and no Rent sysvar account.</summary>
    /// <param name="account">The uninitialized token account.</param>
    /// <param name="mint">The mint the account will hold.</param>
    /// <param name="owner">The account owner encoded in instruction data.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeAccount3 instruction.</returns>
    public static Instruction InitializeAccount3(
        PublicKey account,
        PublicKey mint,
        PublicKey owner,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Readonly(mint)],
            Data = PublicKeyData(InitializeAccount3Discriminator, owner)
        };

    /// <summary>Initializes an SPL Token multisig account and supplies the Rent sysvar.</summary>
    /// <param name="multisig">The uninitialized writable multisig account.</param>
    /// <param name="signerAccounts">The one to eleven possible signer accounts.</param>
    /// <param name="requiredSignatures">The number of member signatures required, from one through the signer count.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeMultisig instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signerAccounts"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The signer count is outside one through eleven, or <paramref name="requiredSignatures"/> is outside one
    /// through the signer count.
    /// </exception>
    public static Instruction InitializeMultisig(
        PublicKey multisig,
        IReadOnlyList<PublicKey> signerAccounts,
        byte requiredSignatures,
        PublicKey? tokenProgram = null)
        => InitializeMultisigCore(
            multisig,
            signerAccounts,
            requiredSignatures,
            includeRent: true,
            InitializeMultisigDiscriminator,
            tokenProgram);

    /// <summary>Initializes an SPL Token multisig account without a Rent sysvar account.</summary>
    /// <param name="multisig">The uninitialized writable multisig account.</param>
    /// <param name="signerAccounts">The one to eleven possible signer accounts.</param>
    /// <param name="requiredSignatures">The number of member signatures required, from one through the signer count.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeMultisig2 instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signerAccounts"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The signer count is outside one through eleven, or <paramref name="requiredSignatures"/> is outside one
    /// through the signer count.
    /// </exception>
    public static Instruction InitializeMultisig2(
        PublicKey multisig,
        IReadOnlyList<PublicKey> signerAccounts,
        byte requiredSignatures,
        PublicKey? tokenProgram = null)
        => InitializeMultisigCore(
            multisig,
            signerAccounts,
            requiredSignatures,
            includeRent: false,
            InitializeMultisig2Discriminator,
            tokenProgram);

    /// <summary>Builds SyncNative with the legacy explicit Rent sysvar account.</summary>
    /// <param name="account">The wrapped-native token account to synchronize.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The syncNative instruction with Rent appended.</returns>
    public static Instruction SyncNativeWithRentSysvar(PublicKey account, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Readonly(RentSysvar)],
            Data = [SyncNativeDiscriminator]
        };

    /// <summary>Requests the required token-account size for a mint through program return data.</summary>
    /// <param name="mint">The mint whose account extensions determine the required size.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The getAccountDataSize instruction.</returns>
    public static Instruction GetAccountDataSize(PublicKey mint, PublicKey? tokenProgram = null)
        => ReadonlyMintInstruction(mint, GetAccountDataSizeDiscriminator, tokenProgram);

    /// <summary>Initializes the immutable-owner extension before initializing a token account.</summary>
    /// <param name="account">The uninitialized writable token account.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The initializeImmutableOwner instruction.</returns>
    public static Instruction InitializeImmutableOwner(PublicKey account, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account)],
            Data = [InitializeImmutableOwnerDiscriminator]
        };

    /// <summary>Requests conversion of a raw token amount to its UI string through program return data.</summary>
    /// <param name="mint">The mint that defines the decimals or Token-2022 scaling rules.</param>
    /// <param name="amount">The raw base-unit amount.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The amountToUiAmount instruction.</returns>
    public static Instruction AmountToUiAmount(PublicKey mint, ulong amount, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Readonly(mint)],
            Data = AmountData(AmountToUiAmountDiscriminator, amount)
        };

    /// <summary>Requests conversion of a UI token amount string to raw units through program return data.</summary>
    /// <param name="mint">The mint that defines the decimals or Token-2022 scaling rules.</param>
    /// <param name="uiAmount">The UTF-8 UI amount string to parse.</param>
    /// <param name="tokenProgram">The token program to target; defaults to classic SPL Token.</param>
    /// <returns>The uiAmountToAmount instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uiAmount"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uiAmount"/> contains invalid Unicode text.</exception>
    public static Instruction UiAmountToAmount(PublicKey mint, string uiAmount, PublicKey? tokenProgram = null)
    {
        ArgumentNullException.ThrowIfNull(uiAmount);

        byte[] encoded;
        try
        {
            encoded = StrictTokenUtf8.GetBytes(uiAmount);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("A token UI amount must contain valid Unicode text.", nameof(uiAmount), exception);
        }

        var data = new byte[encoded.Length + 1];
        data[0] = UiAmountToAmountDiscriminator;
        encoded.CopyTo(data.AsSpan(1));
        return new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Readonly(mint)],
            Data = data
        };
    }

    /// <summary>Withdraws lamports above rent exemption from a Token-program-owned account.</summary>
    /// <param name="account">The writable source account.</param>
    /// <param name="destination">The writable account receiving excess lamports.</param>
    /// <param name="authority">The source account's authority; signs.</param>
    /// <param name="tokenProgram">The token program to target; defaults to Token-2022, whose pinned runtime implements this newer interface instruction.</param>
    /// <returns>The withdrawExcessLamports instruction.</returns>
    public static Instruction WithdrawExcessLamports(
        PublicKey account,
        PublicKey destination,
        PublicKey authority,
        PublicKey? tokenProgram = null)
        => AuthorityLamportInstruction(
            account,
            destination,
            authority,
            [WithdrawExcessLamportsDiscriminator],
            tokenProgram ?? Token2022Program.ProgramId);

    /// <summary>Withdraws excess lamports using a multisig authority.</summary>
    /// <param name="account">The writable source account.</param>
    /// <param name="destination">The writable account receiving excess lamports.</param>
    /// <param name="authority">The multisig authority account.</param>
    /// <param name="tokenProgram">The token program to target, or <c>null</c> for Token-2022.</param>
    /// <param name="multisigSigners">The multisig member accounts that sign, in account order.</param>
    /// <returns>The withdrawExcessLamports instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multisigSigners"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> is empty or contains more than 11 accounts.</exception>
    public static Instruction WithdrawExcessLamports(
        PublicKey account,
        PublicKey destination,
        PublicKey authority,
        PublicKey? tokenProgram,
        IReadOnlyList<PublicKey> multisigSigners)
        => WithMultisigAuthority(
            WithdrawExcessLamports(account, destination, authority, tokenProgram),
            multisigSigners);

    /// <summary>Transfers some or all lamports out of a native wrapped-SOL token account.</summary>
    /// <param name="account">The writable native token account.</param>
    /// <param name="destination">The writable account receiving lamports.</param>
    /// <param name="authority">The native account's authority; signs.</param>
    /// <param name="amount">The number of lamports, or <c>null</c> to transfer the entire available balance.</param>
    /// <param name="tokenProgram">The token program to target; defaults to Token-2022, whose pinned runtime implements this newer interface instruction.</param>
    /// <returns>The unwrapLamports instruction.</returns>
    public static Instruction UnwrapLamports(
        PublicKey account,
        PublicKey destination,
        PublicKey authority,
        ulong? amount = null,
        PublicKey? tokenProgram = null)
        => AuthorityLamportInstruction(
            account,
            destination,
            authority,
            OptionalAmountData(amount),
            tokenProgram ?? Token2022Program.ProgramId);

    /// <summary>Transfers some or all lamports out of a native account using a multisig authority.</summary>
    /// <param name="account">The writable native token account.</param>
    /// <param name="destination">The writable account receiving lamports.</param>
    /// <param name="authority">The multisig authority account.</param>
    /// <param name="amount">The number of lamports, or <c>null</c> to transfer the entire available balance.</param>
    /// <param name="tokenProgram">The token program to target, or <c>null</c> for Token-2022.</param>
    /// <param name="multisigSigners">The multisig member accounts that sign, in account order.</param>
    /// <returns>The unwrapLamports instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multisigSigners"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> is empty or contains more than 11 accounts.</exception>
    public static Instruction UnwrapLamports(
        PublicKey account,
        PublicKey destination,
        PublicKey authority,
        ulong? amount,
        PublicKey? tokenProgram,
        IReadOnlyList<PublicKey> multisigSigners)
        => WithMultisigAuthority(
            UnwrapLamports(account, destination, authority, amount, tokenProgram),
            multisigSigners);

    /// <summary>Combines compatible Token instructions into the Token program's compact batch encoding.</summary>
    /// <param name="instructions">The Token instructions to execute in order.</param>
    /// <param name="tokenProgram">The token program to target; defaults to Token-2022, whose pinned runtime implements batching.</param>
    /// <returns>The batch instruction with concatenated account metas.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> or an element is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The list is empty, an instruction has no data, is itself a batch, targets another program,
    /// or has more than 255 accounts or 255 data bytes.
    /// </exception>
    public static Instruction Batch(IReadOnlyList<Instruction> instructions, PublicKey? tokenProgram = null)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        if (instructions.Count == 0)
            throw new ArgumentException("A Token batch must contain at least one instruction.", nameof(instructions));

        var program = tokenProgram ?? Token2022Program.ProgramId;
        var data = new List<byte> { BatchDiscriminator };
        var accounts = new List<AccountMeta>();

        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i]
                ?? throw new ArgumentNullException(nameof(instructions), $"Instruction at index {i} is null.");
            if (instruction.ProgramId != program)
                throw new ArgumentException($"Instruction at index {i} targets a different program.", nameof(instructions));
            if (instruction.Data.Length == 0)
                throw new ArgumentException($"Instruction at index {i} has no discriminator byte.", nameof(instructions));
            if (instruction.Data is [BatchDiscriminator, ..])
                throw new ArgumentException($"Instruction at index {i} is a nested batch.", nameof(instructions));
            if (instruction.Accounts.Count > byte.MaxValue)
                throw new ArgumentException($"Instruction at index {i} has more than 255 accounts.", nameof(instructions));
            if (instruction.Data.Length > byte.MaxValue)
                throw new ArgumentException($"Instruction at index {i} has more than 255 data bytes.", nameof(instructions));

            data.Add((byte)instruction.Accounts.Count);
            data.Add((byte)instruction.Data.Length);
            data.AddRange(instruction.Data);
            accounts.AddRange(instruction.Accounts);
        }

        return new() { ProgramId = program, Accounts = accounts, Data = [.. data] };
    }

    private static Instruction InitializeMultisigCore(
        PublicKey multisig,
        IReadOnlyList<PublicKey> signerAccounts,
        byte requiredSignatures,
        bool includeRent,
        byte discriminator,
        PublicKey? tokenProgram)
    {
        ArgumentNullException.ThrowIfNull(signerAccounts);
        if (signerAccounts.Count is < 1 or > MaxMultisigSigners)
            throw new ArgumentOutOfRangeException(
                nameof(signerAccounts),
                signerAccounts.Count,
                $"An SPL Token multisig requires between 1 and {MaxMultisigSigners} signer accounts.");
        if (requiredSignatures is 0 || requiredSignatures > signerAccounts.Count)
            throw new ArgumentOutOfRangeException(
                nameof(requiredSignatures),
                requiredSignatures,
                "The required signature count must be between one and the number of signer accounts.");

        var accounts = new List<AccountMeta>(signerAccounts.Count + (includeRent ? 2 : 1))
        {
            AccountMeta.Writable(multisig)
        };
        if (includeRent)
            accounts.Add(AccountMeta.Readonly(RentSysvar));
        for (var i = 0; i < signerAccounts.Count; i++)
            accounts.Add(AccountMeta.Readonly(signerAccounts[i]));

        return new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = accounts,
            Data = [discriminator, requiredSignatures]
        };
    }

    private static Instruction ReadonlyMintInstruction(PublicKey mint, byte discriminator, PublicKey? tokenProgram)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Readonly(mint)],
            Data = [discriminator]
        };

    private static Instruction AuthorityLamportInstruction(
        PublicKey account,
        PublicKey destination,
        PublicKey authority,
        byte[] data,
        PublicKey? tokenProgram)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Writable(destination), AccountMeta.ReadonlySigner(authority)],
            Data = data
        };

    private static byte[] InitializeMintData(
        byte discriminator,
        byte decimals,
        PublicKey mintAuthority,
        PublicKey? freezeAuthority)
    {
        var data = new byte[freezeAuthority is null ? 35 : 67];
        data[0] = discriminator;
        data[1] = decimals;
        mintAuthority.CopyTo(data.AsSpan(2));
        if (freezeAuthority is { } freeze)
        {
            data[34] = 1;
            freeze.CopyTo(data.AsSpan(35));
        }

        return data;
    }

    private static byte[] PublicKeyData(byte discriminator, PublicKey publicKey)
    {
        var data = new byte[33];
        data[0] = discriminator;
        publicKey.CopyTo(data.AsSpan(1));
        return data;
    }

    private static byte[] OptionalAmountData(ulong? amount)
    {
        if (amount is null)
            return [UnwrapLamportsDiscriminator, 0];

        var data = new byte[10];
        data[0] = UnwrapLamportsDiscriminator;
        data[1] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), amount.Value);
        return data;
    }
}
