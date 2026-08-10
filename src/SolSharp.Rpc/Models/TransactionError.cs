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
                if (member.NameEquals("InstructionError")
                    && member.Value.ValueKind == JsonValueKind.Array
                    && member.Value.GetArrayLength() >= 2)
                {
                    return new()
                    {
                        Kind = member.Name,
                        InstructionIndex = member.Value[0].ValueKind == JsonValueKind.Number ? member.Value[0].GetInt32() : null,
                        InstructionError = InstructionError.Parse(member.Value[1]),
                        Details = member.Value.Clone()
                    };
                }

                if (member.NameEquals("DuplicateInstruction") &&
                    member.Value.ValueKind == JsonValueKind.Number &&
                    member.Value.TryGetInt32(out var duplicateInstructionIndex))
                {
                    return new()
                    {
                        Kind = member.Name,
                        DuplicateInstructionIndex = duplicateInstructionIndex,
                        Details = member.Value.Clone()
                    };
                }

                if ((member.NameEquals("InsufficientFundsForRent") ||
                     member.NameEquals("ProgramExecutionTemporarilyRestricted")) &&
                    member.Value.ValueKind == JsonValueKind.Object &&
                    member.Value.TryGetProperty("account_index", out var accountIndex) &&
                    accountIndex.ValueKind == JsonValueKind.Number &&
                    accountIndex.TryGetInt32(out var accountIndexValue))
                {
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
    public static InstructionError Parse(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
            return new() { Kind = error.GetString()! };

        if (error.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in error.EnumerateObject())
            {
                return member.NameEquals("Custom") && member.Value.ValueKind == JsonValueKind.Number
                    ? new() { Kind = "Custom", CustomCode = member.Value.GetUInt32() }
                    : new InstructionError { Kind = member.Name };
            }
        }

        return new() { Kind = error.ToString() };
    }

    /// <inheritdoc/>
    public override string ToString() => CustomCode is { } code ? $"Custom({code})" : Kind;
}
