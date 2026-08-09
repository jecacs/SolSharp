using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The most recent blockhash and the last block height at which it stays valid.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getlatestblockhash">getLatestBlockhash</seealso>
public sealed record LatestBlockhash
{
    private string? _blockhash;

    /// <summary>The base58-encoded recent blockhash to set on a transaction.</summary>
    [JsonPropertyName("blockhash")]
    [JsonRequired]
    public string Blockhash
    {
        get => _blockhash ?? throw new InvalidOperationException("The latest blockhash has not been initialized.");
        init => _blockhash = value ?? throw new JsonException("A latest-blockhash result must carry a blockhash.");
    }

    /// <summary>The last block height at which <see cref="Blockhash"/> is still accepted.</summary>
    [JsonPropertyName("lastValidBlockHeight")]
    [JsonRequired]
    public ulong LastValidBlockHeight { get; init; }
}
