using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

// Resolves compiled instructions back into Instructions for both message formats. The combined account index
// space is: static keys, then loaded-writable, then loaded-readonly (for legacy there is no loaded section).
internal static class MessageDecompiler
{
    public static IReadOnlyList<Instruction> Decompile(
        IReadOnlyList<CompiledInstruction> instructions,
        IReadOnlyList<PublicKey> keys,
        int numSigners,
        int numReadonlySigned,
        int numReadonlyUnsigned,
        int numStatic,
        int numLoadedWritable)
    {
        var result = new Instruction[instructions.Count];
        for (var n = 0; n < instructions.Count; n++)
        {
            var compiled = instructions[n];
            var accounts = new AccountMeta[compiled.AccountIndexes.Length];
            for (var a = 0; a < compiled.AccountIndexes.Length; a++)
            {
                int index = compiled.AccountIndexes[a];
                accounts[a] = new AccountMeta(
                    KeyAt(keys, index),
                    IsSigner(index, numSigners),
                    IsWritable(index, numSigners, numReadonlySigned, numReadonlyUnsigned, numStatic, numLoadedWritable));
            }

            result[n] = new Instruction
            {
                ProgramId = KeyAt(keys, compiled.ProgramIdIndex),
                Accounts = accounts,
                Data = compiled.Data
            };
        }

        return result;
    }

    private static PublicKey KeyAt(IReadOnlyList<PublicKey> keys, int index)
        => index < keys.Count
            ? keys[index]
            : throw new ArgumentException($"Account index {index} is out of range for {keys.Count} resolved keys.");

    private static bool IsSigner(int index, int numSigners) => index < numSigners;

    private static bool IsWritable(int index, int numSigners, int numReadonlySigned, int numReadonlyUnsigned, int numStatic, int numLoadedWritable)
    {
        if (index < numSigners)
            return index < numSigners - numReadonlySigned;     // signer: writable signers lead the signers
        if (index < numStatic)
            return index < numStatic - numReadonlyUnsigned;    // static non-signer: writable lead the non-signers
        return index < numStatic + numLoadedWritable;          // loaded: writable section precedes the read-only section
    }
}
