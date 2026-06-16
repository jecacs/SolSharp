using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for the System program: lamport transfers and account creation.</summary>
public static class SystemProgram
{
    /// <summary>The System program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.SystemProgram);

    private const uint TransferDiscriminator = 2;
    private const uint CreateAccountDiscriminator = 0;

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
}
