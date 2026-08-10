using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for Solana's Loader V4 program-management interface.</summary>
public static class LoaderV4Program
{
    /// <summary>The minimum slot cooldown between deployment-state changes.</summary>
    public const ulong DeploymentCooldownSlots = 1;

    /// <summary>The Loader V4 program address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("LoaderV411111111111111111111111111111111111");

    private const uint WriteDiscriminator = 0;
    private const uint CopyDiscriminator = 1;
    private const uint SetProgramLengthDiscriminator = 2;
    private const uint DeployDiscriminator = 3;
    private const uint RetractDiscriminator = 4;
    private const uint TransferAuthorityDiscriminator = 5;
    private const uint FinalizeDiscriminator = 6;

    /// <summary>Creates a Loader V4 account and initializes its program-data length.</summary>
    /// <param name="payer">The funding signer.</param>
    /// <param name="programAccount">The new program-account signer.</param>
    /// <param name="lamports">The lamports to fund.</param>
    /// <param name="authority">The program-authority signer.</param>
    /// <param name="programLength">The initial program-data length.</param>
    /// <param name="recipient">The writable recipient of any excess lamports.</param>
    /// <returns>The System create instruction followed by set-program-length.</returns>
    public static Instruction[] CreateBuffer(
        PublicKey payer,
        PublicKey programAccount,
        ulong lamports,
        PublicKey authority,
        uint programLength,
        PublicKey recipient)
        =>
        [
            SystemProgram.CreateAccount(payer, programAccount, lamports, 0, ProgramId),
            SetProgramLength(programAccount, authority, programLength, recipient)
        ];

    /// <summary>Changes the size of a retracted program account.</summary>
    /// <param name="programAccount">The writable program account.</param>
    /// <param name="authority">The program-authority signer.</param>
    /// <param name="newLength">The new program-data length.</param>
    /// <param name="recipient">The writable account receiving excess lamports.</param>
    /// <returns>The set-program-length instruction.</returns>
    public static Instruction SetProgramLength(
        PublicKey programAccount,
        PublicKey authority,
        uint newLength,
        PublicKey recipient)
        => CreateInstruction(
            ProgramWireEncoding.Build(
                SetProgramLengthDiscriminator,
                stream => ProgramWireEncoding.WriteUInt32(stream, newLength)),
            [
                AccountMeta.Writable(programAccount),
                AccountMeta.ReadonlySigner(authority),
                AccountMeta.Writable(recipient)
            ]);

    /// <summary>Writes ELF bytes into a retracted program account.</summary>
    /// <param name="programAccount">The writable program account.</param>
    /// <param name="authority">The program-authority signer.</param>
    /// <param name="offset">The destination byte offset.</param>
    /// <param name="bytes">The ELF bytes.</param>
    /// <returns>The write instruction.</returns>
    public static Instruction Write(
        PublicKey programAccount,
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
            [AccountMeta.Writable(programAccount), AccountMeta.ReadonlySigner(authority)]);
    }

    /// <summary>Copies ELF bytes from another program account.</summary>
    /// <param name="programAccount">The writable destination program.</param>
    /// <param name="authority">The destination program-authority signer.</param>
    /// <param name="sourceProgram">The read-only source program.</param>
    /// <param name="destinationOffset">The destination byte offset.</param>
    /// <param name="sourceOffset">The source byte offset.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <returns>The copy instruction.</returns>
    public static Instruction Copy(
        PublicKey programAccount,
        PublicKey authority,
        PublicKey sourceProgram,
        uint destinationOffset,
        uint sourceOffset,
        uint length)
        => CreateInstruction(
            ProgramWireEncoding.Build(CopyDiscriminator, stream =>
            {
                ProgramWireEncoding.WriteUInt32(stream, destinationOffset);
                ProgramWireEncoding.WriteUInt32(stream, sourceOffset);
                ProgramWireEncoding.WriteUInt32(stream, length);
            }),
            [
                AccountMeta.Writable(programAccount),
                AccountMeta.ReadonlySigner(authority),
                AccountMeta.Readonly(sourceProgram)
            ]);

    /// <summary>Verifies and deploys a program, optionally consuming a source program account.</summary>
    /// <param name="programAccount">The writable program to deploy.</param>
    /// <param name="authority">The program-authority signer.</param>
    /// <param name="sourceProgram">An optional writable source whose bytes and lamports are consumed.</param>
    /// <returns>The deploy instruction.</returns>
    public static Instruction Deploy(
        PublicKey programAccount,
        PublicKey authority,
        PublicKey? sourceProgram = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(programAccount),
            AccountMeta.ReadonlySigner(authority)
        };
        if (sourceProgram is { } source)
            accounts.Add(AccountMeta.Writable(source));
        return CreateInstruction(ProgramWireEncoding.Build(DeployDiscriminator), accounts);
    }

    /// <summary>Retracts a deployed program into writable maintenance mode.</summary>
    /// <param name="programAccount">The writable deployed program.</param>
    /// <param name="authority">The program-authority signer.</param>
    /// <returns>The retract instruction.</returns>
    public static Instruction Retract(PublicKey programAccount, PublicKey authority)
        => CreateInstruction(
            ProgramWireEncoding.Build(RetractDiscriminator),
            [AccountMeta.Writable(programAccount), AccountMeta.ReadonlySigner(authority)]);

    /// <summary>Transfers management authority to a new signer.</summary>
    /// <param name="programAccount">The writable program account.</param>
    /// <param name="authority">The current authority signer.</param>
    /// <param name="newAuthority">The replacement authority signer.</param>
    /// <returns>The transfer-authority instruction.</returns>
    public static Instruction TransferAuthority(
        PublicKey programAccount,
        PublicKey authority,
        PublicKey newAuthority)
        => CreateInstruction(
            ProgramWireEncoding.Build(TransferAuthorityDiscriminator),
            [
                AccountMeta.Writable(programAccount),
                AccountMeta.ReadonlySigner(authority),
                AccountMeta.ReadonlySigner(newAuthority)
            ]);

    /// <summary>Finalizes a program permanently and records its next version.</summary>
    /// <param name="programAccount">The writable program account.</param>
    /// <param name="authority">The current authority signer.</param>
    /// <param name="nextVersionProgram">The read-only next-version program, which may be the program itself.</param>
    /// <returns>The finalize instruction.</returns>
    public static Instruction Finalize(
        PublicKey programAccount,
        PublicKey authority,
        PublicKey nextVersionProgram)
        => CreateInstruction(
            ProgramWireEncoding.Build(FinalizeDiscriminator),
            [
                AccountMeta.Writable(programAccount),
                AccountMeta.ReadonlySigner(authority),
                AccountMeta.Readonly(nextVersionProgram)
            ]);

    private static Instruction CreateInstruction(byte[] data, IReadOnlyList<AccountMeta> accounts)
        => new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
}
