using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Shared readers and sanitize checks for the parts of the message wire format common to legacy and v0
/// messages.
/// </summary>
internal static class MessageWire
{
    /// <summary>Reads a compact-u16-prefixed list of 32-byte account keys, advancing <paramref name="offset"/>.</summary>
    public static PublicKey[] ReadAccountKeys(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = ShortVec.Decode(data[offset..], out var read);
        offset += read;
        EnsureMinimumBytes(data.Length - offset, count, PublicKey.Length, "account key");

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
        // Even an instruction with no accounts and no data needs a program-id byte and two
        // one-byte zero length prefixes. Reject impossible declared counts before allocating.
        EnsureMinimumBytes(data.Length - offset, count, 3, "instruction");

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

    private static void EnsureMinimumBytes(int remainingBytes, int count, int minimumElementBytes, string elementName)
    {
        var minimumBytes = (long)count * minimumElementBytes;
        if (minimumBytes > remainingBytes)
            throw new FormatException(
                $"The wire data declares {count} {elementName}(s), which need at least {minimumBytes} byte(s), but only {remainingBytes} byte(s) remain.");
    }

    /// <summary>
    /// Enforces the header rules of Solana's sanitize: the signing area and the read-only non-signing area
    /// must fit the account list without overlapping, and at least one signer must be writable so it can
    /// pay the fee.
    /// </summary>
    public static void SanitizeHeader(byte requiredSignatures, byte readonlySignedAccounts, byte readonlyUnsignedAccounts, int accountKeyCount)
    {
        if (requiredSignatures + readonlyUnsignedAccounts > accountKeyCount)
            throw new FormatException(
                $"The header claims {requiredSignatures} signer(s) and {readonlyUnsignedAccounts} read-only unsigned account(s), but the message has only {accountKeyCount} account key(s).");

        if (readonlySignedAccounts >= requiredSignatures)
            throw new FormatException(
                $"The header marks {readonlySignedAccounts} of {requiredSignatures} signer(s) read-only; the fee payer must be a writable signer.");
    }

    /// <summary>
    /// Enforces the instruction rules of Solana's sanitize: every program id index must point at a
    /// directly listed account other than the fee payer, and every account index must stay inside the
    /// accounts the message can address.
    /// </summary>
    public static void SanitizeInstructions(CompiledInstruction[] instructions, int staticAccountCount, int addressableAccountCount)
    {
        foreach (var instruction in instructions)
        {
            // Program ids must come from the message's own key list - never a lookup table - so
            // instructions can be checked without loading on-chain data. For a legacy message the
            // two bounds coincide.
            if (instruction.ProgramIdIndex >= staticAccountCount)
                throw new FormatException(
                    $"Instruction program id index {instruction.ProgramIdIndex} is out of range: a program id must be one of the message's {staticAccountCount} account key(s).");

            if (instruction.ProgramIdIndex == 0)
                throw new FormatException("Instruction program id index 0 refers to the fee payer; a program cannot be the fee payer.");

            foreach (var index in instruction.AccountIndexes)
                if (index >= addressableAccountCount)
                    throw new FormatException(
                        $"Instruction account index {index} is out of range: the message addresses {addressableAccountCount} account(s).");
        }
    }
}
