using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for Solana's upgradeable BPF Loader 3 interface.</summary>
public static class UpgradeableBpfLoaderProgram
{
    /// <summary>The general minimum extension after activation of SIMD-0431.</summary>
    public const uint MinimumExtendProgramBytes = 10_240;

    /// <summary>The upgradeable BPF Loader 3 address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("BPFLoaderUpgradeab1e11111111111111111111111");

    private const uint InitializeBufferDiscriminator = 0;
    private const uint WriteDiscriminator = 1;
    private const uint DeployDiscriminator = 2;
    private const uint UpgradeDiscriminator = 3;
    private const uint SetAuthorityDiscriminator = 4;
    private const uint CloseDiscriminator = 5;
    private const uint ExtendProgramDiscriminator = 6;
    private const uint SetAuthorityCheckedDiscriminator = 7;

    private static readonly PublicKey ClockSysvar = PublicKey.Parse(Sysvars.Clock);
    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);

    /// <summary>Derives the ProgramData PDA belonging to an executable Program account.</summary>
    /// <param name="programAccount">The executable Program account.</param>
    /// <returns>The canonical ProgramData PDA.</returns>
    public static PublicKey GetProgramDataAddress(PublicKey programAccount)
        => ProgramDerivedAddress.FindProgramAddress([programAccount.ToBytes()], ProgramId).Address;

    /// <summary>Creates and initializes a writable program-data buffer.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="buffer">The new buffer-account signer.</param>
    /// <param name="authority">The initial buffer authority.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="programLength">The maximum bytes stored after buffer metadata.</param>
    /// <returns>The System create instruction followed by buffer initialization.</returns>
    public static Instruction[] CreateBuffer(
        PublicKey payer,
        PublicKey buffer,
        PublicKey authority,
        ulong lamports,
        ulong programLength)
    {
        var space = checked(UpgradeableBpfLoaderState.BufferMetadataLength + programLength);
        return
        [
            SystemProgram.CreateAccount(payer, buffer, lamports, space, ProgramId),
            InitializeBuffer(buffer, authority)
        ];
    }

    /// <summary>Initializes an already allocated buffer account.</summary>
    /// <param name="buffer">The writable buffer account.</param>
    /// <param name="authority">The optional, non-signing initial authority.</param>
    /// <returns>The initialize-buffer instruction.</returns>
    public static Instruction InitializeBuffer(PublicKey buffer, PublicKey? authority)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(buffer) };
        if (authority is { } authorityAddress)
            accounts.Add(AccountMeta.Readonly(authorityAddress));
        return CreateInstruction(ProgramWireEncoding.Build(InitializeBufferDiscriminator), accounts);
    }

    /// <summary>Writes program bytes into a buffer.</summary>
    /// <param name="buffer">The writable buffer account.</param>
    /// <param name="authority">The buffer-authority signer.</param>
    /// <param name="offset">The program-data byte offset.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <returns>The write instruction.</returns>
    public static Instruction Write(
        PublicKey buffer,
        PublicKey authority,
        uint offset,
        ReadOnlySpan<byte> bytes)
    {
        var payload = bytes.ToArray();
        return CreateInstruction(
            ProgramWireEncoding.Build(WriteDiscriminator, stream =>
            {
                ProgramWireEncoding.WriteUInt32(stream, offset);
                ProgramWireEncoding.WriteByteVector(stream, payload);
            }),
            [AccountMeta.Writable(buffer), AccountMeta.ReadonlySigner(authority)]);
    }

    /// <summary>Creates a Program account and deploys the contents of a buffer.</summary>
    /// <param name="payer">The writable funding signer for ProgramData creation.</param>
    /// <param name="programAccount">The new executable Program-account signer.</param>
    /// <param name="buffer">The writable source buffer.</param>
    /// <param name="upgradeAuthority">The upgrade-authority signer.</param>
    /// <param name="programLamports">The lamports used to create the Program account.</param>
    /// <param name="maximumProgramLength">The maximum program-data length.</param>
    /// <param name="closeBuffer">Whether deployment should close the source buffer.</param>
    /// <returns>The System create instruction followed by the deploy instruction.</returns>
    public static Instruction[] DeployWithMaximumProgramLength(
        PublicKey payer,
        PublicKey programAccount,
        PublicKey buffer,
        PublicKey upgradeAuthority,
        ulong programLamports,
        ulong maximumProgramLength,
        bool closeBuffer = true)
        =>
        [
            SystemProgram.CreateAccount(
                payer,
                programAccount,
                programLamports,
                UpgradeableBpfLoaderState.ProgramMetadataLength,
                ProgramId),
            DeployInstruction(
                payer,
                programAccount,
                buffer,
                upgradeAuthority,
                maximumProgramLength,
                closeBuffer)
        ];

    /// <summary>Builds the deploy instruction for an already allocated Program account.</summary>
    /// <param name="payer">The writable funding signer for ProgramData creation.</param>
    /// <param name="programAccount">The writable Program account.</param>
    /// <param name="buffer">The writable source buffer.</param>
    /// <param name="upgradeAuthority">The upgrade-authority signer.</param>
    /// <param name="maximumProgramLength">The maximum program-data length.</param>
    /// <param name="closeBuffer">Whether deployment should close the source buffer.</param>
    /// <returns>The deploy instruction.</returns>
    public static Instruction DeployInstruction(
        PublicKey payer,
        PublicKey programAccount,
        PublicKey buffer,
        PublicKey upgradeAuthority,
        ulong maximumProgramLength,
        bool closeBuffer = true)
        => CreateInstruction(
            ProgramWireEncoding.Build(DeployDiscriminator, stream =>
            {
                ProgramWireEncoding.WriteUInt64(stream, maximumProgramLength);
                ProgramWireEncoding.WriteByte(stream, closeBuffer ? (byte)1 : (byte)0);
            }),
            [
                AccountMeta.WritableSigner(payer),
                AccountMeta.Writable(GetProgramDataAddress(programAccount)),
                AccountMeta.Writable(programAccount),
                AccountMeta.Writable(buffer),
                AccountMeta.Readonly(RentSysvar),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.Readonly(SystemProgram.ProgramId),
                AccountMeta.ReadonlySigner(upgradeAuthority)
            ]);

    /// <summary>Upgrades a program from a populated buffer.</summary>
    /// <param name="programAccount">The writable executable Program account.</param>
    /// <param name="buffer">The writable source buffer.</param>
    /// <param name="authority">The upgrade-authority signer.</param>
    /// <param name="spill">The writable recipient of excess lamports.</param>
    /// <param name="closeBuffer">Whether the upgrade should close the source buffer.</param>
    /// <returns>The upgrade instruction.</returns>
    public static Instruction Upgrade(
        PublicKey programAccount,
        PublicKey buffer,
        PublicKey authority,
        PublicKey spill,
        bool closeBuffer = true)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                UpgradeDiscriminator,
                stream => ProgramWireEncoding.WriteByte(stream, closeBuffer ? (byte)1 : (byte)0)),
            [
                AccountMeta.Writable(GetProgramDataAddress(programAccount)),
                AccountMeta.Writable(programAccount),
                AccountMeta.Writable(buffer),
                AccountMeta.Writable(spill),
                AccountMeta.Readonly(RentSysvar),
                AccountMeta.Readonly(ClockSysvar),
                AccountMeta.ReadonlySigner(authority)
            ]);

    /// <summary>Changes a buffer authority without requiring the replacement key to sign.</summary>
    /// <param name="buffer">The writable buffer.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority.</param>
    /// <returns>The set-authority instruction.</returns>
    public static Instruction SetBufferAuthority(
        PublicKey buffer,
        PublicKey currentAuthority,
        PublicKey newAuthority)
        => SetAuthority(buffer, currentAuthority, newAuthority, isChecked: false);

    /// <summary>Changes a buffer authority and requires the replacement key to sign.</summary>
    /// <param name="buffer">The writable buffer.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <returns>The checked set-authority instruction.</returns>
    public static Instruction SetBufferAuthorityChecked(
        PublicKey buffer,
        PublicKey currentAuthority,
        PublicKey newAuthority)
        => SetAuthority(buffer, currentAuthority, newAuthority, isChecked: true);

    /// <summary>Changes or permanently revokes a program's upgrade authority.</summary>
    /// <param name="programAccount">The executable Program account used to derive ProgramData.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority, or <c>null</c> to make the program immutable.</param>
    /// <returns>The set-authority instruction.</returns>
    public static Instruction SetUpgradeAuthority(
        PublicKey programAccount,
        PublicKey currentAuthority,
        PublicKey? newAuthority)
        => SetAuthority(GetProgramDataAddress(programAccount), currentAuthority, newAuthority, isChecked: false);

    /// <summary>Changes a program's upgrade authority and requires the replacement key to sign.</summary>
    /// <param name="programAccount">The executable Program account used to derive ProgramData.</param>
    /// <param name="currentAuthority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <returns>The checked set-authority instruction.</returns>
    public static Instruction SetUpgradeAuthorityChecked(
        PublicKey programAccount,
        PublicKey currentAuthority,
        PublicKey newAuthority)
        => SetAuthority(
            GetProgramDataAddress(programAccount),
            currentAuthority,
            newAuthority,
            isChecked: true);

    /// <summary>Closes a buffer and transfers its lamports to a recipient.</summary>
    /// <param name="buffer">The writable buffer to close.</param>
    /// <param name="recipient">The writable lamport recipient.</param>
    /// <param name="authority">The buffer-authority signer.</param>
    /// <returns>The close instruction.</returns>
    public static Instruction CloseBuffer(
        PublicKey buffer,
        PublicKey recipient,
        PublicKey authority)
        => Close(buffer, recipient, authority, null, tombstone: false);

    /// <summary>Closes an upgradeable-loader account or tombstones an executable program.</summary>
    /// <param name="account">The writable account to close, or ProgramData when closing a program.</param>
    /// <param name="recipient">The writable lamport recipient.</param>
    /// <param name="authority">The optional authority signer.</param>
    /// <param name="associatedProgram">The optional writable Program account paired with ProgramData.</param>
    /// <param name="tombstone">Whether to tombstone the associated Program address.</param>
    /// <returns>The close instruction.</returns>
    public static Instruction Close(
        PublicKey account,
        PublicKey recipient,
        PublicKey? authority,
        PublicKey? associatedProgram,
        bool tombstone = false)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(account),
            AccountMeta.Writable(recipient)
        };
        if (authority is { } authorityAddress)
            accounts.Add(AccountMeta.ReadonlySigner(authorityAddress));
        if (associatedProgram is { } programAddress)
            accounts.Add(AccountMeta.Writable(programAddress));

        return CreateInstruction(
            ProgramWireEncoding.Build(
                CloseDiscriminator,
                stream => ProgramWireEncoding.WriteByte(stream, tombstone ? (byte)1 : (byte)0)),
            accounts);
    }

    /// <summary>Extends a ProgramData account.</summary>
    /// <param name="programAccount">The writable Program account used to derive ProgramData.</param>
    /// <param name="additionalBytes">The number of bytes to add.</param>
    /// <param name="payer">An optional writable funding signer.</param>
    /// <returns>The extend-program instruction.</returns>
    public static Instruction ExtendProgram(
        PublicKey programAccount,
        uint additionalBytes,
        PublicKey? payer = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(GetProgramDataAddress(programAccount)),
            AccountMeta.Writable(programAccount)
        };
        if (payer is { } payerAddress)
        {
            accounts.Add(AccountMeta.Readonly(SystemProgram.ProgramId));
            accounts.Add(AccountMeta.WritableSigner(payerAddress));
        }

        return CreateInstruction(
            ProgramWireEncoding.Build(
                ExtendProgramDiscriminator,
                stream => ProgramWireEncoding.WriteUInt32(stream, additionalBytes)),
            accounts);
    }

    private static Instruction SetAuthority(
        PublicKey account,
        PublicKey currentAuthority,
        PublicKey? newAuthority,
        bool isChecked)
    {
        if (isChecked && newAuthority is null)
            throw new ArgumentNullException(nameof(newAuthority));

        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(account),
            AccountMeta.ReadonlySigner(currentAuthority)
        };
        if (newAuthority is { } authorityAddress)
        {
            accounts.Add(isChecked
                ? AccountMeta.ReadonlySigner(authorityAddress)
                : AccountMeta.Readonly(authorityAddress));
        }

        return CreateInstruction(
            ProgramWireEncoding.Build(
                isChecked ? SetAuthorityCheckedDiscriminator : SetAuthorityDiscriminator),
            accounts);
    }

    private static Instruction CreateInstruction(byte[] data, IReadOnlyList<AccountMeta> accounts)
        => new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
}
