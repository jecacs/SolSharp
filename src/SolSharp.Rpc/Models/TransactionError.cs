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

    /// <summary>Decodes a node's <c>err</c> value; returns <c>null</c> for a successful transaction (no error).</summary>
    /// <param name="err">The raw <c>err</c> JSON, or <c>null</c>.</param>
    /// <returns>The decoded error, or <c>null</c> when there is none.</returns>
    public static TransactionError? Parse(JsonElement? err)
    {
        if (err is not { } value || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return new TransactionError { Kind = value.GetString()! };

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in value.EnumerateObject())
            {
                if (member.NameEquals("InstructionError")
                    && member.Value.ValueKind == JsonValueKind.Array
                    && member.Value.GetArrayLength() >= 2)
                {
                    return new TransactionError
                    {
                        Kind = member.Name,
                        InstructionIndex = member.Value[0].ValueKind == JsonValueKind.Number ? member.Value[0].GetInt32() : null,
                        InstructionError = global::SolSharp.Rpc.Models.InstructionError.Parse(member.Value[1])
                    };
                }

                return new TransactionError { Kind = member.Name };
            }
        }

        return new TransactionError { Kind = value.ToString() };
    }

    /// <inheritdoc/>
    public override string ToString()
        => InstructionError is { } inner
            ? $"InstructionError at instruction {InstructionIndex}: {inner}"
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
            return new InstructionError { Kind = error.GetString()! };

        if (error.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in error.EnumerateObject())
            {
                return member.NameEquals("Custom") && member.Value.ValueKind == JsonValueKind.Number
                    ? new InstructionError { Kind = "Custom", CustomCode = member.Value.GetUInt32() }
                    : new InstructionError { Kind = member.Name };
            }
        }

        return new InstructionError { Kind = error.ToString() };
    }

    /// <inheritdoc/>
    public override string ToString() => CustomCode is { } code ? $"Custom({code})" : Kind;
}
