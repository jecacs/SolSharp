using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Builds instructions for the System program: lamport transfers, account creation and allocation,
/// owner assignment, and durable nonce management.
/// </summary>
public static class SystemProgram
{
    /// <summary>The System program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.SystemProgram);

    private const uint CreateAccountDiscriminator = 0;
    private const uint AssignDiscriminator = 1;
    private const uint TransferDiscriminator = 2;
    private const uint CreateAccountWithSeedDiscriminator = 3;
    private const uint AdvanceNonceAccountDiscriminator = 4;
    private const uint WithdrawNonceAccountDiscriminator = 5;
    private const uint InitializeNonceAccountDiscriminator = 6;
    private const uint AuthorizeNonceAccountDiscriminator = 7;
    private const uint AllocateDiscriminator = 8;
    private const uint AllocateWithSeedDiscriminator = 9;
    private const uint AssignWithSeedDiscriminator = 10;
    private const uint TransferWithSeedDiscriminator = 11;

    /// <summary>The serialized size of a durable nonce account, in bytes (80).</summary>
    public const int NonceAccountLength = 80;

    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);
    private static readonly PublicKey RecentBlockhashesSysvar = PublicKey.Parse(Sysvars.RecentBlockhashes);

    /// <summary>Builds a transfer of <paramref name="lamports"/> lamports from one account to another.</summary>
    /// <param name="from">The funding account; signs the transaction and is debited.</param>
    /// <param name="to">The account that receives the lamports.</param>
    /// <param name="lamports">The amount to transfer, in lamports.</param>
    /// <returns>The transfer instruction.</returns>
    public static Instruction Transfer(PublicKey from, PublicKey to, ulong lamports)
    {
        var data = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(data, TransferDiscriminator);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), lamports);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.WritableSigner(from), AccountMeta.Writable(to)],
            Data = data
        };
    }

    /// <summary>Builds an instruction that creates a new account, funds it, and assigns its owner.</summary>
    /// <param name="from">The funding account; signs the transaction and pays for the new account.</param>
    /// <param name="newAccount">The address of the account to create; must also sign.</param>
    /// <param name="lamports">The lamports to deposit into the new account (typically the rent-exempt minimum).</param>
    /// <param name="space">The number of bytes to allocate for the account's data.</param>
    /// <param name="owner">The program that will own the new account.</param>
    /// <returns>The create-account instruction.</returns>
    public static Instruction CreateAccount(PublicKey from, PublicKey newAccount, ulong lamports, ulong space, PublicKey owner)
    {
        var data = new byte[52];
        BinaryPrimitives.WriteUInt32LittleEndian(data, CreateAccountDiscriminator);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), lamports);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(12), space);
        owner.CopyTo(data.AsSpan(20));

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.WritableSigner(from), AccountMeta.WritableSigner(newAccount)],
            Data = data
        };
    }

    /// <summary>
    /// Creates an account at an address derived from a base key and a seed (<c>create_with_seed</c>), funds it,
    /// and assigns its owner. The base account signs in place of the created address.
    /// </summary>
    /// <param name="from">The funding account; signs the transaction and pays for the new account.</param>
    /// <param name="createdAccount">The derived address being created (writable, does not sign).</param>
    /// <param name="baseAccount">The base key the address was derived from; signs the transaction.</param>
    /// <param name="seed">The seed the address was derived with.</param>
    /// <param name="lamports">The lamports to deposit into the new account.</param>
    /// <param name="space">The number of bytes to allocate for the account's data.</param>
    /// <param name="owner">The program that will own the new account.</param>
    /// <returns>The createAccountWithSeed instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <c>null</c>.</exception>
    public static Instruction CreateAccountWithSeed(
        PublicKey from,
        PublicKey createdAccount,
        PublicKey baseAccount,
        string seed,
        ulong lamports,
        ulong space,
        PublicKey owner)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);

        using var buffer = new MemoryStream(52 + seedBytes.Length);
        Span<byte> word = stackalloc byte[8];

        BinaryPrimitives.WriteUInt32LittleEndian(word, CreateAccountWithSeedDiscriminator);
        buffer.Write(word[..4]);
        buffer.Write(baseAccount.ToBytes());
        BinaryPrimitives.WriteUInt64LittleEndian(word, (ulong)seedBytes.Length); // bincode string: u64 length prefix
        buffer.Write(word);
        buffer.Write(seedBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(word, lamports);
        buffer.Write(word);
        BinaryPrimitives.WriteUInt64LittleEndian(word, space);
        buffer.Write(word);
        buffer.Write(owner.ToBytes());

        var accounts = new List<AccountMeta>(3)
        {
            AccountMeta.WritableSigner(from),
            AccountMeta.Writable(createdAccount)
        };
        if (baseAccount != from)
            accounts.Add(AccountMeta.ReadonlySigner(baseAccount));

        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = buffer.ToArray() };
    }

    /// <summary>Assigns a new owner program to an existing (system-owned) account.</summary>
    /// <param name="account">The account to reassign; signs the transaction.</param>
    /// <param name="owner">The program to set as the new owner.</param>
    /// <returns>The assign instruction.</returns>
    public static Instruction Assign(PublicKey account, PublicKey owner)
    {
        var data = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(data, AssignDiscriminator);
        owner.CopyTo(data.AsSpan(4));

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.WritableSigner(account)],
            Data = data
        };
    }

    /// <summary>Allocates <paramref name="space"/> bytes of data for an existing (system-owned) account.</summary>
    /// <param name="account">The account to allocate space for; signs the transaction.</param>
    /// <param name="space">The number of bytes to allocate.</param>
    /// <returns>The allocate instruction.</returns>
    public static Instruction Allocate(PublicKey account, ulong space)
    {
        var data = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(data, AllocateDiscriminator);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), space);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.WritableSigner(account)],
            Data = data
        };
    }

    /// <summary>
    /// Allocates <paramref name="space"/> bytes for an account at an address derived from a base key and a
    /// seed (<c>create_with_seed</c>). The base account signs in place of the derived address.
    /// </summary>
    /// <param name="account">The derived address to allocate space for (writable, does not sign).</param>
    /// <param name="baseAccount">The base key the address was derived from; signs the transaction.</param>
    /// <param name="seed">The seed the address was derived with.</param>
    /// <param name="space">The number of bytes to allocate.</param>
    /// <param name="owner">The program that will own the account.</param>
    /// <returns>The allocateWithSeed instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <c>null</c>.</exception>
    public static Instruction AllocateWithSeed(PublicKey account, PublicKey baseAccount, string seed, ulong space, PublicKey owner)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);

        using var buffer = new MemoryStream(84 + seedBytes.Length);
        Span<byte> word = stackalloc byte[8];

        BinaryPrimitives.WriteUInt32LittleEndian(word, AllocateWithSeedDiscriminator);
        buffer.Write(word[..4]);
        buffer.Write(baseAccount.ToBytes());
        BinaryPrimitives.WriteUInt64LittleEndian(word, (ulong)seedBytes.Length); // bincode string: u64 length prefix
        buffer.Write(word);
        buffer.Write(seedBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(word, space);
        buffer.Write(word);
        buffer.Write(owner.ToBytes());

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.ReadonlySigner(baseAccount)],
            Data = buffer.ToArray()
        };
    }

    /// <summary>
    /// Assigns a new owner program to an account at an address derived from a base key and a seed
    /// (<c>create_with_seed</c>). The base account signs in place of the derived address.
    /// </summary>
    /// <param name="account">The derived address to reassign (writable, does not sign).</param>
    /// <param name="baseAccount">The base key the address was derived from; signs the transaction.</param>
    /// <param name="seed">The seed the address was derived with.</param>
    /// <param name="owner">The program to set as the new owner.</param>
    /// <returns>The assignWithSeed instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <c>null</c>.</exception>
    public static Instruction AssignWithSeed(PublicKey account, PublicKey baseAccount, string seed, PublicKey owner)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);

        using var buffer = new MemoryStream(76 + seedBytes.Length);
        Span<byte> word = stackalloc byte[8];

        BinaryPrimitives.WriteUInt32LittleEndian(word, AssignWithSeedDiscriminator);
        buffer.Write(word[..4]);
        buffer.Write(baseAccount.ToBytes());
        BinaryPrimitives.WriteUInt64LittleEndian(word, (ulong)seedBytes.Length); // bincode string: u64 length prefix
        buffer.Write(word);
        buffer.Write(seedBytes);
        buffer.Write(owner.ToBytes());

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.ReadonlySigner(baseAccount)],
            Data = buffer.ToArray()
        };
    }

    /// <summary>
    /// Transfers lamports from an account at an address derived from a base key and a seed
    /// (<c>create_with_seed</c>). The base account signs in place of the derived address.
    /// </summary>
    /// <param name="from">The derived funding address; debited (writable, does not sign).</param>
    /// <param name="baseAccount">The base key <paramref name="from"/> was derived from; signs the transaction.</param>
    /// <param name="seed">The seed <paramref name="from"/> was derived with.</param>
    /// <param name="owner">The owner program <paramref name="from"/> was derived with.</param>
    /// <param name="to">The account that receives the lamports.</param>
    /// <param name="lamports">The amount to transfer, in lamports.</param>
    /// <returns>The transferWithSeed instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <c>null</c>.</exception>
    public static Instruction TransferWithSeed(
        PublicKey from,
        PublicKey baseAccount,
        string seed,
        PublicKey owner,
        PublicKey to,
        ulong lamports)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);

        using var buffer = new MemoryStream(52 + seedBytes.Length);
        Span<byte> word = stackalloc byte[8];

        BinaryPrimitives.WriteUInt32LittleEndian(word, TransferWithSeedDiscriminator);
        buffer.Write(word[..4]);
        BinaryPrimitives.WriteUInt64LittleEndian(word, lamports);
        buffer.Write(word);
        BinaryPrimitives.WriteUInt64LittleEndian(word, (ulong)seedBytes.Length); // bincode string: u64 length prefix
        buffer.Write(word);
        buffer.Write(seedBytes);
        buffer.Write(owner.ToBytes());

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(from),
                AccountMeta.ReadonlySigner(baseAccount),
                AccountMeta.Writable(to)
            ],
            Data = buffer.ToArray()
        };
    }

    /// <summary>
    /// Builds the two instructions that create and initialize a durable nonce account: a
    /// <see cref="CreateAccount"/> of <see cref="NonceAccountLength"/> bytes owned by the System program,
    /// followed by <see cref="InitializeNonceAccount"/>.
    /// </summary>
    /// <param name="payer">The funding account; signs the transaction and pays for the nonce account.</param>
    /// <param name="nonceAccount">The address of the nonce account to create; must also sign.</param>
    /// <param name="authority">The authority allowed to advance and withdraw the nonce.</param>
    /// <param name="lamports">The lamports to deposit (the rent-exempt minimum for <see cref="NonceAccountLength"/> bytes).</param>
    /// <returns>The create-account instruction followed by the initialize-nonce instruction.</returns>
    public static Instruction[] CreateNonceAccount(PublicKey payer, PublicKey nonceAccount, PublicKey authority, ulong lamports)
        =>
        [
            CreateAccount(payer, nonceAccount, lamports, NonceAccountLength, ProgramId),
            InitializeNonceAccount(nonceAccount, authority)
        ];

    /// <summary>Initializes a created account as a durable nonce account controlled by <paramref name="authority"/>.</summary>
    /// <param name="nonceAccount">The account to initialize as a nonce account (writable).</param>
    /// <param name="authority">The authority allowed to advance and withdraw the nonce.</param>
    /// <returns>The initializeNonceAccount instruction.</returns>
    public static Instruction InitializeNonceAccount(PublicKey nonceAccount, PublicKey authority)
    {
        var data = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(data, InitializeNonceAccountDiscriminator);
        authority.CopyTo(data.AsSpan(4));

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(nonceAccount),
                AccountMeta.Readonly(RecentBlockhashesSysvar),
                AccountMeta.Readonly(RentSysvar)
            ],
            Data = data
        };
    }

    /// <summary>Advances a durable nonce account to its next nonce; runs as the first instruction of a nonce-anchored transaction.</summary>
    /// <param name="nonceAccount">The nonce account to advance (writable).</param>
    /// <param name="authority">The nonce authority; signs.</param>
    /// <returns>The advanceNonceAccount instruction.</returns>
    public static Instruction AdvanceNonceAccount(PublicKey nonceAccount, PublicKey authority)
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, AdvanceNonceAccountDiscriminator);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(nonceAccount),
                AccountMeta.Readonly(RecentBlockhashesSysvar),
                AccountMeta.ReadonlySigner(authority)
            ],
            Data = data
        };
    }

    /// <summary>Withdraws <paramref name="lamports"/> lamports from a durable nonce account.</summary>
    /// <param name="nonceAccount">The nonce account to withdraw from (writable).</param>
    /// <param name="authority">The nonce authority; signs.</param>
    /// <param name="recipient">The account that receives the lamports (writable).</param>
    /// <param name="lamports">The amount to withdraw, in lamports.</param>
    /// <returns>The withdrawNonceAccount instruction.</returns>
    public static Instruction WithdrawNonceAccount(PublicKey nonceAccount, PublicKey authority, PublicKey recipient, ulong lamports)
    {
        var data = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(data, WithdrawNonceAccountDiscriminator);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), lamports);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(nonceAccount),
                AccountMeta.Writable(recipient),
                AccountMeta.Readonly(RecentBlockhashesSysvar),
                AccountMeta.Readonly(RentSysvar),
                AccountMeta.ReadonlySigner(authority)
            ],
            Data = data
        };
    }

    /// <summary>Changes a durable nonce account's authority to <paramref name="newAuthority"/>.</summary>
    /// <param name="nonceAccount">The nonce account to re-authorize (writable).</param>
    /// <param name="authority">The current nonce authority; signs.</param>
    /// <param name="newAuthority">The new authority to set.</param>
    /// <returns>The authorizeNonceAccount instruction.</returns>
    public static Instruction AuthorizeNonceAccount(PublicKey nonceAccount, PublicKey authority, PublicKey newAuthority)
    {
        var data = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(data, AuthorizeNonceAccountDiscriminator);
        newAuthority.CopyTo(data.AsSpan(4));

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(nonceAccount), AccountMeta.ReadonlySigner(authority)],
            Data = data
        };
    }
}
