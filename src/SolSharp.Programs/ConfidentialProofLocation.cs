using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Identifies precomputed zero-knowledge proof data used by a Token-2022 instruction: either a
/// non-zero relative transaction-instruction offset or a pre-verified proof-context account.
/// This type describes only the proof location; it does not generate or validate cryptographic proofs.
/// </summary>
public sealed class ConfidentialProofLocation
{
    private ConfidentialProofLocation(sbyte instructionOffset, PublicKey? contextStateAccount)
    {
        InstructionOffset = instructionOffset;
        ContextStateAccount = contextStateAccount;
    }

    /// <summary>Whether the proof is supplied by another instruction in the same transaction.</summary>
    public bool IsInstructionOffset => ContextStateAccount is null;

    /// <summary>
    /// The non-zero relative proof-instruction offset, or zero when <see cref="ContextStateAccount"/>
    /// is used.
    /// </summary>
    public sbyte InstructionOffset { get; }

    /// <summary>The pre-verified proof-context account, or <c>null</c> when an instruction offset is used.</summary>
    public PublicKey? ContextStateAccount { get; }

    /// <summary>Uses a precomputed proof verification instruction at a relative transaction offset.</summary>
    /// <param name="instructionOffset">A non-zero signed relative instruction offset.</param>
    /// <returns>The proof location.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="instructionOffset"/> is zero.</exception>
    public static ConfidentialProofLocation AtInstructionOffset(sbyte instructionOffset)
    {
        if (instructionOffset == 0)
            throw new ArgumentOutOfRangeException(
                nameof(instructionOffset),
                instructionOffset,
                "A proof instruction offset must be non-zero; zero denotes a context-state account on the wire.");

        return new(instructionOffset, contextStateAccount: null);
    }

    /// <summary>Uses a proof that was pre-verified into a context-state account.</summary>
    /// <param name="contextStateAccount">The proof-context account.</param>
    /// <returns>The proof location.</returns>
    public static ConfidentialProofLocation AtContextState(PublicKey contextStateAccount)
        => new(instructionOffset: 0, contextStateAccount);
}
