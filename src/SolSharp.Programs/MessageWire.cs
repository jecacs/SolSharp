using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Shared readers for the parts of the message wire format common to legacy and v0 messages.</summary>
internal static class MessageWire
{
    /// <summary>Reads a compact-u16-prefixed list of 32-byte account keys, advancing <paramref name="offset"/>.</summary>
    public static PublicKey[] ReadAccountKeys(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = ShortVec.Decode(data[offset..], out var read);
        offset += read;

        var keys = new PublicKey[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = new PublicKey(data.Slice(offset, PublicKey.Length));
            offset += PublicKey.Length;
        }

        return keys;
    }

    /// <summary>Reads a compact-u16-prefixed list of compiled instructions, advancing <paramref name="offset"/>.</summary>
    public static CompiledInstruction[] ReadInstructions(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = ShortVec.Decode(data[offset..], out var read);
        offset += read;

        var instructions = new CompiledInstruction[count];
        for (var i = 0; i < count; i++)
        {
            var programIdIndex = data[offset++];

            var accountCount = ShortVec.Decode(data[offset..], out read);
            offset += read;
            var accountIndexes = data.Slice(offset, accountCount).ToArray();
            offset += accountCount;

            var dataLength = ShortVec.Decode(data[offset..], out read);
            offset += read;
            var instructionData = data.Slice(offset, dataLength).ToArray();
            offset += dataLength;

            instructions[i] = new CompiledInstruction
            {
                ProgramIdIndex = programIdIndex,
                AccountIndexes = accountIndexes,
                Data = instructionData
            };
        }

        return instructions;
    }
}
