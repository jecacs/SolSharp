namespace SolSharp.Programs;

/// <summary>
/// An instruction compiled to account indices, as it appears inside a serialized <see cref="Message"/>:
/// the program and accounts are referenced by their position in the message's account list.
/// </summary>
public sealed class CompiledInstruction
{
    /// <summary>Index into the message's account keys of the program to invoke.</summary>
    public required byte ProgramIdIndex { get; init; }

    /// <summary>Indices into the message's account keys of the instruction's accounts, in order.</summary>
    public required byte[] AccountIndexes { get; init; }

    /// <summary>The instruction's data payload.</summary>
    public required byte[] Data { get; init; }
}
