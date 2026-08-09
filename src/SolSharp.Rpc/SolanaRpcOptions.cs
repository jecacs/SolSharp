using System.ComponentModel.DataAnnotations;

namespace SolSharp.Rpc;

/// <summary>Configuration for <see cref="SolanaRpcClient"/>.</summary>
public sealed class SolanaRpcOptions
{
    /// <summary>The default maximum HTTP response body size: 128 MiB.</summary>
    public const int DefaultMaximumResponseContentLength = 128 * 1024 * 1024;

    /// <summary>The JSON-RPC HTTP endpoint the client posts to.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string Endpoint { get; set; } = "https://api.mainnet-beta.solana.com";

    /// <summary>
    /// Maximum decoded HTTP response body size, in bytes. Defaults to 128 MiB so large block and program-account
    /// responses remain usable while an unbounded or unexpectedly large response cannot exhaust process memory.
    /// </summary>
    public int MaximumResponseContentLength { get; set; } = DefaultMaximumResponseContentLength;
}
