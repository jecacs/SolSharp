using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>The inner (CPI) instructions invoked under one top-level instruction of a <c>jsonParsed</c> transaction.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedInnerInstructions
{
    /// <summary>The index of the top-level instruction these inner instructions were invoked from.</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>The inner instructions, in invocation order.</summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<ParsedInstruction> Instructions { get; init; } = [];
}
