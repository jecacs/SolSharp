using System.ComponentModel.DataAnnotations;

namespace SolSharp.Rpc;

/// <summary>Configuration for <see cref="SolanaRpcClient"/>.</summary>
public sealed class SolanaRpcOptions
{
    /// <summary>The JSON-RPC HTTP endpoint the client posts to.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string Endpoint { get; set; } = "https://api.mainnet-beta.solana.com";
}
