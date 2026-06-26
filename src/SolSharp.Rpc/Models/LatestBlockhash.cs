using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The most recent blockhash and the last block height at which it stays valid.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getlatestblockhash">getLatestBlockhash</seealso>
public sealed record LatestBlockhash
{
    /// <summary>The base58-encoded recent blockhash to set on a transaction.</summary>
    [JsonPropertyName("blockhash")]
    public string Blockhash { get; init; } = string.Empty;

    /// <summary>The last block height at which <see cref="Blockhash"/> is still accepted.</summary>
    [JsonPropertyName("lastValidBlockHeight")]
    public ulong LastValidBlockHeight { get; init; }
}
