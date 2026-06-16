using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

public sealed record LatestBlockhash
{
    [JsonPropertyName("blockhash")]
    public string Blockhash { get; init; } = string.Empty;

    [JsonPropertyName("lastValidBlockHeight")]
    public ulong LastValidBlockHeight { get; init; }
}
