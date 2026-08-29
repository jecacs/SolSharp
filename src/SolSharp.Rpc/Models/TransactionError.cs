using System.Text.Json;

namespace SolSharp.Rpc.Models;

/// <summary>
/// A decoded transaction error - the typed form of the raw <c>err</c> a node reports. The common case is an
/// <see cref="InstructionError"/> at a specific instruction, which for a program failure carries the program's
/// own <see cref="InstructionError.CustomCode"/> (for example an Anchor error or an AMM slippage code).
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record TransactionError
{
    /// <summary>The error variant, e.g. <c>InstructionError</c>, <c>AccountInUse</c>, <c>BlockhashNotFound</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The index of the failing instruction, when <see cref="Kind"/> is <c>InstructionError</c>.</summary>
    public int? InstructionIndex { get; init; }

    /// <summary>The instruction-level error, when <see cref="Kind"/> is <c>InstructionError</c>.</summary>
    public InstructionError? InstructionError { get; init; }

    /// <summary>
    /// The duplicate top-level instruction index, when <see cref="Kind"/> is <c>DuplicateInstruction</c>.
    /// </summary>
    public int? DuplicateInstructionIndex { get; init; }

    /// <summary>
    /// The account index, when <see cref="Kind"/> is <c>InsufficientFundsForRent</c> or
    /// <c>ProgramExecutionTemporarilyRestricted</c>.
    /// </summary>
    public int? AccountIndex { get; init; }

    /// <summary>The raw payload of a parameterized variant, retained for forward compatibility.</summary>
    public JsonElement? Details { get; init; }

    /// <summary>Decodes a node's <c>err</c> value; returns <c>null</c> for a successful transaction (no error).</summary>
    /// <param name="err">The raw <c>err</c> JSON, or <c>null</c>.</param>
    /// <returns>The decoded error, or <c>null</c> when there is none.</returns>
    /// <exception cref="JsonException">A known parameterized variant has an invalid wire shape or numeric range.</exception>
    public static TransactionError? Parse(JsonElement? err)
    {
        if (err is not { } value || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return new() { Kind = value.GetString()! };

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in value.EnumerateObject())
            {
                if (member.NameEquals("InstructionError"))
                {
                    if (member.Value.ValueKind != JsonValueKind.Array ||
                        member.Value.GetArrayLength() != 2 ||
                        member.Value[0].ValueKind != JsonValueKind.Number ||
                        !member.Value[0].TryGetByte(out var instructionIndex))
                    {
                        throw new JsonException("InstructionError must be a two-element tuple with a u8 instruction index.");
                    }

                    return new()
                    {
                        Kind = member.Name,
                        InstructionIndex = instructionIndex,
                        InstructionError = InstructionError.Parse(member.Value[1]),
                        Details = member.Value.Clone()
                    };
                }

                if (member.NameEquals("DuplicateInstruction"))
                {
                    if (member.Value.ValueKind != JsonValueKind.Number ||
                        !member.Value.TryGetByte(out var duplicateInstructionIndex))
                    {
                        throw new JsonException("DuplicateInstruction must carry a u8 instruction index.");
                    }

                    return new()
                    {
                        Kind = member.Name,
                        DuplicateInstructionIndex = duplicateInstructionIndex,
                        Details = member.Value.Clone()
                    };
                }

                if (member.NameEquals("InsufficientFundsForRent") ||
                    member.NameEquals("ProgramExecutionTemporarilyRestricted"))
                {
                    if (member.Value.ValueKind != JsonValueKind.Object ||
                        !member.Value.TryGetProperty("account_index", out var accountIndex) ||
                        accountIndex.ValueKind != JsonValueKind.Number ||
                        !accountIndex.TryGetByte(out var accountIndexValue))
                    {
                        throw new JsonException($"{member.Name} must carry a u8 account_index.");
                    }

                    return new()
                    {
                        Kind = member.Name,
                        AccountIndex = accountIndexValue,
                        Details = member.Value.Clone()
                    };
                }

                return new() { Kind = member.Name, Details = member.Value.Clone() };
            }
        }

        return new() { Kind = value.ToString() };
    }

    /// <inheritdoc/>
    public override string ToString()
        => InstructionError is { } inner
            ? $"InstructionError at instruction {InstructionIndex}: {inner}"
            : DuplicateInstructionIndex is { } duplicateInstructionIndex
                ? $"DuplicateInstruction at instruction {duplicateInstructionIndex}"
                : AccountIndex is { } accountIndex
                    ? $"{Kind} at account {accountIndex}"
                    : Kind;
}

/// <summary>An instruction-level error - a named runtime variant, or a program-defined <see cref="CustomCode"/>.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record InstructionError
{
    /// <summary>The error variant, e.g. <c>Custom</c>, <c>InsufficientFunds</c>, <c>InvalidAccountData</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The program's own error code, when <see cref="Kind"/> is <c>Custom</c>.</summary>
    public uint? CustomCode { get; init; }

    /// <summary>Decodes the instruction-error half of an <c>InstructionError</c> tuple.</summary>
    /// <param name="error">The raw instruction-error JSON (a string variant or a single-key object).</param>
    /// <returns>The decoded instruction error.</returns>
    /// <exception cref="JsonException">The <c>Custom</c> variant does not carry a u32 value.</exception>
    public static InstructionError Parse(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
            return new() { Kind = error.GetString()! };

        if (error.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in error.EnumerateObject())
            {
                if (!member.NameEquals("Custom"))
                    return new() { Kind = member.Name };

                if (member.Value.ValueKind != JsonValueKind.Number ||
                    !member.Value.TryGetUInt32(out var customCode))
                {
                    throw new JsonException("Custom instruction errors must carry a u32 code.");
                }

                return new() { Kind = "Custom", CustomCode = customCode };
            }
        }

        return new() { Kind = error.ToString() };
    }

    /// <inheritdoc/>
    public override string ToString() => CustomCode is { } code ? $"Custom({code})" : Kind;
}
