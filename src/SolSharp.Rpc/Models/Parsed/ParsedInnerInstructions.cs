using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>The inner (CPI) instructions invoked under one top-level instruction of a <c>jsonParsed</c> transaction.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedInnerInstructions
{
    /// <summary>The index of the top-level instruction these inner instructions were invoked from.</summary>
    [JsonPropertyName("index")]
    [JsonRequired]
    public byte Index { get; init; }

    /// <summary>The inner instructions, in invocation order.</summary>
    [JsonPropertyName("instructions")]
    [JsonRequired]
    public IReadOnlyList<ParsedInstruction> Instructions
    {
        get => field!;
        init
        {
            if (value is null || value.Any(static instruction => instruction is null))
                throw new JsonException("Parsed inner instructions must carry only non-null instructions.");

            field = value;
        }
    }
}
