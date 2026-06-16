using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A single Solana instruction: the program to invoke, the accounts it touches (in the order the program
/// expects), and its opaque, program-specific data payload.
/// </summary>
public sealed class Instruction
{
    /// <summary>The program that executes this instruction.</summary>
    public required PublicKey ProgramId { get; init; }

    /// <summary>The accounts the instruction reads or writes, in program-defined order.</summary>
    public required IReadOnlyList<AccountMeta> Accounts { get; init; }

    /// <summary>The instruction's data payload, already encoded for the program.</summary>
    public required byte[] Data { get; init; }
}
