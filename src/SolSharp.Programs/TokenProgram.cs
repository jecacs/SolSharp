using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Builds instructions for the SPL Token program: transfers, mint and burn, approve and revoke, freeze and
/// thaw, account initialization and close, and wrapped-SOL sync. Every builder takes an optional
/// <c>tokenProgram</c> so the same instructions can target Token-2022 (the layouts are shared); it defaults
/// to the classic SPL Token program.
/// </summary>
public static class TokenProgram
{
    /// <summary>The SPL Token program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.TokenProgram);

    /// <summary>The instruction-data discriminator (first data byte) of InitializeMint.</summary>
    public const byte InitializeMintDiscriminator = 0;

    /// <summary>The instruction-data discriminator (first data byte) of InitializeAccount.</summary>
    public const byte InitializeAccountDiscriminator = 1;

    /// <summary>The instruction-data discriminator (first data byte) of Transfer.</summary>
    public const byte TransferDiscriminator = 3;

    /// <summary>The instruction-data discriminator (first data byte) of Approve.</summary>
    public const byte ApproveDiscriminator = 4;

    /// <summary>The instruction-data discriminator (first data byte) of Revoke.</summary>
    public const byte RevokeDiscriminator = 5;

    /// <summary>The instruction-data discriminator (first data byte) of SetAuthority.</summary>
    public const byte SetAuthorityDiscriminator = 6;

    /// <summary>The instruction-data discriminator (first data byte) of MintTo.</summary>
    public const byte MintToDiscriminator = 7;

    /// <summary>The instruction-data discriminator (first data byte) of Burn.</summary>
    public const byte BurnDiscriminator = 8;

    /// <summary>The instruction-data discriminator (first data byte) of CloseAccount.</summary>
    public const byte CloseAccountDiscriminator = 9;

    /// <summary>The instruction-data discriminator (first data byte) of FreezeAccount.</summary>
    public const byte FreezeAccountDiscriminator = 10;

    /// <summary>The instruction-data discriminator (first data byte) of ThawAccount.</summary>
    public const byte ThawAccountDiscriminator = 11;

    /// <summary>The instruction-data discriminator (first data byte) of TransferChecked.</summary>
    public const byte TransferCheckedDiscriminator = 12;

    /// <summary>The instruction-data discriminator (first data byte) of ApproveChecked.</summary>
    public const byte ApproveCheckedDiscriminator = 13;

    /// <summary>The instruction-data discriminator (first data byte) of MintToChecked.</summary>
    public const byte MintToCheckedDiscriminator = 14;

    /// <summary>The instruction-data discriminator (first data byte) of BurnChecked.</summary>
    public const byte BurnCheckedDiscriminator = 15;

    /// <summary>The instruction-data discriminator (first data byte) of SyncNative.</summary>
    public const byte SyncNativeDiscriminator = 17;

    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);

    /// <summary>
    /// Builds an (unchecked) token transfer of <paramref name="amount"/> base units. Prefer
    /// <see cref="TransferChecked"/>, which also verifies the mint and its decimals.
    /// </summary>
    /// <param name="source">The source token account; debited.</param>
    /// <param name="destination">The destination token account; credited.</param>
    /// <param name="authority">The source account's owner or delegate; signs the transaction.</param>
    /// <param name="amount">The amount to transfer, in the token's base units.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The transfer instruction.</returns>
    public static Instruction Transfer(PublicKey source, PublicKey destination, PublicKey authority, ulong amount, PublicKey? tokenProgram = null)
    {
        var data = new byte[9];
        data[0] = TransferDiscriminator;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), amount);

        return new Instruction
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(source),
                AccountMeta.Writable(destination),
                AccountMeta.ReadonlySigner(authority)
            ],
            Data = data
        };
    }

    /// <summary>Builds a checked token transfer, which also verifies the mint and its decimals - the recommended form.</summary>
    /// <param name="source">The source token account; debited.</param>
    /// <param name="mint">The token mint; verified by the program.</param>
    /// <param name="destination">The destination token account; credited.</param>
    /// <param name="authority">The source account's owner or delegate; signs the transaction.</param>
    /// <param name="amount">The amount to transfer, in the token's base units.</param>
    /// <param name="decimals">The mint's decimals; must match the on-chain mint.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The checked transfer instruction.</returns>
    public static Instruction TransferChecked(
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount,
        byte decimals,
        PublicKey? tokenProgram = null)
    {
        var data = new byte[10];
        data[0] = TransferCheckedDiscriminator;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), amount);
        data[9] = decimals;

        return new Instruction
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(source),
                AccountMeta.Readonly(mint),
                AccountMeta.Writable(destination),
                AccountMeta.ReadonlySigner(authority)
            ],
            Data = data
        };
    }

    /// <summary>Mints <paramref name="amount"/> new base units to a token account.</summary>
    /// <param name="mint">The mint to mint from (writable).</param>
    /// <param name="destination">The token account to credit (writable).</param>
    /// <param name="authority">The mint authority; signs.</param>
    /// <param name="amount">The amount to mint, in base units.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The mintTo instruction.</returns>
    public static Instruction MintTo(PublicKey mint, PublicKey destination, PublicKey authority, ulong amount, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(mint), AccountMeta.Writable(destination), AccountMeta.ReadonlySigner(authority)],
            Data = AmountData(MintToDiscriminator, amount)
        };

    /// <summary>Burns <paramref name="amount"/> base units from a token account.</summary>
    /// <param name="account">The token account to debit (writable).</param>
    /// <param name="mint">The token mint (writable).</param>
    /// <param name="authority">The account's owner or delegate; signs.</param>
    /// <param name="amount">The amount to burn, in base units.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The burn instruction.</returns>
    public static Instruction Burn(PublicKey account, PublicKey mint, PublicKey authority, ulong amount, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Writable(mint), AccountMeta.ReadonlySigner(authority)],
            Data = AmountData(BurnDiscriminator, amount)
        };

    /// <summary>Approves a delegate to transfer up to <paramref name="amount"/> base units from a token account.</summary>
    /// <param name="source">The token account to delegate from (writable).</param>
    /// <param name="delegate">The delegate authorized to transfer.</param>
    /// <param name="owner">The account's owner; signs.</param>
    /// <param name="amount">The maximum amount the delegate may transfer, in base units.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The approve instruction.</returns>
    public static Instruction Approve(PublicKey source, PublicKey @delegate, PublicKey owner, ulong amount, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(source), AccountMeta.Readonly(@delegate), AccountMeta.ReadonlySigner(owner)],
            Data = AmountData(ApproveDiscriminator, amount)
        };

    /// <summary>Revokes a token account's current delegate.</summary>
    /// <param name="source">The token account whose delegate is revoked (writable).</param>
    /// <param name="owner">The account's owner; signs.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The revoke instruction.</returns>
    public static Instruction Revoke(PublicKey source, PublicKey owner, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(source), AccountMeta.ReadonlySigner(owner)],
            Data = [RevokeDiscriminator]
        };

    /// <summary>
    /// Closes a token account and sends its rent lamports to <paramref name="destination"/>. The token balance
    /// must be zero first (use this on an emptied or native account, e.g. to unwrap wSOL).
    /// </summary>
    /// <param name="account">The token account to close (writable).</param>
    /// <param name="destination">The account that receives the reclaimed lamports (writable).</param>
    /// <param name="owner">The account's owner; signs.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The closeAccount instruction.</returns>
    public static Instruction CloseAccount(PublicKey account, PublicKey destination, PublicKey owner, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Writable(destination), AccountMeta.ReadonlySigner(owner)],
            Data = [CloseAccountDiscriminator]
        };

    /// <summary>Syncs a native (wrapped SOL) token account's token balance to its underlying lamports.</summary>
    /// <param name="account">The native token account to sync (writable).</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The syncNative instruction.</returns>
    public static Instruction SyncNative(PublicKey account, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account)],
            Data = [SyncNativeDiscriminator]
        };

    /// <summary>Freezes a token account, blocking transfers until it is thawed.</summary>
    /// <param name="account">The token account to freeze (writable).</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="authority">The mint's freeze authority; signs.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The freezeAccount instruction.</returns>
    public static Instruction FreezeAccount(PublicKey account, PublicKey mint, PublicKey authority, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Readonly(mint), AccountMeta.ReadonlySigner(authority)],
            Data = [FreezeAccountDiscriminator]
        };

    /// <summary>Thaws a frozen token account.</summary>
    /// <param name="account">The token account to thaw (writable).</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="authority">The mint's freeze authority; signs.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The thawAccount instruction.</returns>
    public static Instruction ThawAccount(PublicKey account, PublicKey mint, PublicKey authority, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Readonly(mint), AccountMeta.ReadonlySigner(authority)],
            Data = [ThawAccountDiscriminator]
        };

    /// <summary>Initializes a previously-created account as a token account for <paramref name="mint"/>.</summary>
    /// <param name="account">The uninitialized account to initialize (writable).</param>
    /// <param name="mint">The mint the account will hold.</param>
    /// <param name="owner">The account's owner.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The initializeAccount instruction.</returns>
    public static Instruction InitializeAccount(PublicKey account, PublicKey mint, PublicKey owner, PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(account),
                AccountMeta.Readonly(mint),
                AccountMeta.Readonly(owner),
                AccountMeta.Readonly(RentSysvar)
            ],
            Data = [InitializeAccountDiscriminator]
        };

    /// <summary>Initializes a previously-created account as a token mint.</summary>
    /// <param name="mint">The uninitialized account to initialize as a mint (writable).</param>
    /// <param name="decimals">The number of base-unit decimal places.</param>
    /// <param name="mintAuthority">The authority allowed to mint tokens.</param>
    /// <param name="freezeAuthority">The authority allowed to freeze accounts, or <c>null</c> for none.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The initializeMint instruction.</returns>
    public static Instruction InitializeMint(PublicKey mint, byte decimals, PublicKey mintAuthority, PublicKey? freezeAuthority = null, PublicKey? tokenProgram = null)
    {
        // data: discriminator, decimals, mint authority (32), then a compact COption freeze authority
        // (1-byte tag, plus 32 bytes when present) - the minimal form spl-token packs.
        using var buffer = new MemoryStream(67);
        buffer.WriteByte(InitializeMintDiscriminator);
        buffer.WriteByte(decimals);
        buffer.Write(mintAuthority.ToBytes());
        if (freezeAuthority is { } freeze)
        {
            buffer.WriteByte(1);
            buffer.Write(freeze.ToBytes());
        }
        else
        {
            buffer.WriteByte(0);
        }

        return new Instruction
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(mint), AccountMeta.Readonly(RentSysvar)],
            Data = buffer.ToArray()
        };
    }

    /// <summary>
    /// Changes one of an account's authorities: a mint's mint or freeze authority, or a token account's
    /// owner or close authority.
    /// </summary>
    /// <param name="account">The mint or token account whose authority changes (writable).</param>
    /// <param name="currentAuthority">The authority being replaced; signs.</param>
    /// <param name="authorityType">Which authority to change.</param>
    /// <param name="newAuthority">The new authority, or <c>null</c> to remove the authority permanently.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The setAuthority instruction.</returns>
    public static Instruction SetAuthority(
        PublicKey account,
        PublicKey currentAuthority,
        AuthorityType authorityType,
        PublicKey? newAuthority = null,
        PublicKey? tokenProgram = null)
    {
        // The new authority is a compact instruction COption (a 1-byte tag, plus the key only when present) -
        // the form the Rust spl-token builder packs. (solana-py pads None with 32 zero bytes; both unpack.)
        var data = new byte[newAuthority is null ? 3 : 35];
        data[0] = SetAuthorityDiscriminator;
        data[1] = (byte)authorityType;
        if (newAuthority is { } authority)
        {
            data[2] = 1;
            authority.CopyTo(data.AsSpan(3));
        }

        return new Instruction
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.ReadonlySigner(currentAuthority)],
            Data = data
        };
    }

    /// <summary>Approves a delegate for up to <paramref name="amount"/> base units, also verifying the mint and its decimals - the recommended form.</summary>
    /// <param name="source">The token account to delegate from (writable).</param>
    /// <param name="mint">The token mint; verified by the program.</param>
    /// <param name="delegate">The delegate authorized to transfer.</param>
    /// <param name="owner">The account's owner; signs.</param>
    /// <param name="amount">The maximum amount the delegate may transfer, in base units.</param>
    /// <param name="decimals">The mint's decimals; must match the on-chain mint.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The approveChecked instruction.</returns>
    public static Instruction ApproveChecked(
        PublicKey source,
        PublicKey mint,
        PublicKey @delegate,
        PublicKey owner,
        ulong amount,
        byte decimals,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(source),
                AccountMeta.Readonly(mint),
                AccountMeta.Readonly(@delegate),
                AccountMeta.ReadonlySigner(owner)
            ],
            Data = CheckedData(ApproveCheckedDiscriminator, amount, decimals)
        };

    /// <summary>Mints new base units to a token account, also verifying the mint's decimals - the recommended form.</summary>
    /// <param name="mint">The mint to mint from (writable).</param>
    /// <param name="destination">The token account to credit (writable).</param>
    /// <param name="authority">The mint authority; signs.</param>
    /// <param name="amount">The amount to mint, in base units.</param>
    /// <param name="decimals">The mint's decimals; must match the on-chain mint.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The mintToChecked instruction.</returns>
    public static Instruction MintToChecked(
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount,
        byte decimals,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(mint), AccountMeta.Writable(destination), AccountMeta.ReadonlySigner(authority)],
            Data = CheckedData(MintToCheckedDiscriminator, amount, decimals)
        };

    /// <summary>Burns base units from a token account, also verifying the mint's decimals - the recommended form.</summary>
    /// <param name="account">The token account to debit (writable).</param>
    /// <param name="mint">The token mint (writable).</param>
    /// <param name="authority">The account's owner or delegate; signs.</param>
    /// <param name="amount">The amount to burn, in base units.</param>
    /// <param name="decimals">The mint's decimals; must match the on-chain mint.</param>
    /// <param name="tokenProgram">The token program to target; defaults to the classic SPL Token program. Pass <c>SolanaProgramIds.Token2022Program</c> for Token-2022.</param>
    /// <returns>The burnChecked instruction.</returns>
    public static Instruction BurnChecked(
        PublicKey account,
        PublicKey mint,
        PublicKey authority,
        ulong amount,
        byte decimals,
        PublicKey? tokenProgram = null)
        => new()
        {
            ProgramId = tokenProgram ?? ProgramId,
            Accounts = [AccountMeta.Writable(account), AccountMeta.Writable(mint), AccountMeta.ReadonlySigner(authority)],
            Data = CheckedData(BurnCheckedDiscriminator, amount, decimals)
        };

    private static byte[] AmountData(byte discriminator, ulong amount)
    {
        var data = new byte[9];
        data[0] = discriminator;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), amount);
        return data;
    }

    private static byte[] CheckedData(byte discriminator, ulong amount, byte decimals)
    {
        var data = new byte[10];
        data[0] = discriminator;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), amount);
        data[9] = decimals;
        return data;
    }
}
