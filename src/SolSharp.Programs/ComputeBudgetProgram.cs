using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Builds instructions for the Compute Budget program: the compute-unit limit and the priority fee.
/// A latency-sensitive sender usually adds both near the front of the transaction.
/// </summary>
public static class ComputeBudgetProgram
{
    /// <summary>The Compute Budget program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.ComputeBudgetProgram);

    private const byte RequestHeapFrameDiscriminator = 1;
    private const byte SetComputeUnitLimitDiscriminator = 2;
    private const byte SetComputeUnitPriceDiscriminator = 3;
    private const byte SetLoadedAccountsDataSizeLimitDiscriminator = 4;

    /// <summary>Sets the maximum number of compute units the transaction may consume.</summary>
    /// <param name="units">The compute-unit limit.</param>
    /// <returns>The instruction (it references no accounts).</returns>
    public static Instruction SetComputeUnitLimit(uint units)
    {
        var data = new byte[5];
        data[0] = SetComputeUnitLimitDiscriminator;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(1), units);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [],
            Data = data
        };
    }

    /// <summary>Sets the priority fee as a price per compute unit, in micro-lamports.</summary>
    /// <param name="microLamports">The price per compute unit, in micro-lamports.</param>
    /// <returns>The instruction (it references no accounts).</returns>
    public static Instruction SetComputeUnitPrice(ulong microLamports)
    {
        var data = new byte[9];
        data[0] = SetComputeUnitPriceDiscriminator;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), microLamports);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [],
            Data = data
        };
    }

    /// <summary>
    /// Builds both compute-budget instructions for a priority fee: the compute-unit limit followed by the
    /// price per unit. Add them near the front of the transaction (e.g. via <c>TransactionBuilder.AddInstructions</c>).
    /// </summary>
    /// <param name="computeUnitLimit">The compute-unit limit to set.</param>
    /// <param name="microLamportsPerComputeUnit">The price per compute unit, in micro-lamports.</param>
    /// <returns>The limit instruction followed by the price instruction.</returns>
    public static Instruction[] SetPriorityFee(uint computeUnitLimit, ulong microLamportsPerComputeUnit)
        => [SetComputeUnitLimit(computeUnitLimit), SetComputeUnitPrice(microLamportsPerComputeUnit)];

    /// <summary>
    /// Requests a transaction-wide heap frame of <paramref name="bytes"/> bytes. The runtime accepts a
    /// multiple of 1024 between 32 KiB (the default) and 256 KiB.
    /// </summary>
    /// <param name="bytes">The heap size to request, in bytes.</param>
    /// <returns>The instruction (it references no accounts).</returns>
    public static Instruction RequestHeapFrame(uint bytes)
    {
        var data = new byte[5];
        data[0] = RequestHeapFrameDiscriminator;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(1), bytes);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [],
            Data = data
        };
    }

    /// <summary>
    /// Caps the total serialized size of the accounts the transaction may load, in bytes. A tighter cap than
    /// the 64 MiB default lowers the transaction's loaded-accounts cost.
    /// </summary>
    /// <param name="bytes">The loaded-accounts data-size limit, in bytes.</param>
    /// <returns>The instruction (it references no accounts).</returns>
    public static Instruction SetLoadedAccountsDataSizeLimit(uint bytes)
    {
        var data = new byte[5];
        data[0] = SetLoadedAccountsDataSizeLimitDiscriminator;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(1), bytes);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [],
            Data = data
        };
    }
}
