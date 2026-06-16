using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Protocol;

/// <summary>Builds the JSON-RPC request for each supported method.</summary>
public static class RpcRequests
{
    public static RpcRequest GetLatestBlockhash(Commitment commitment) =>
        new()
        {
            Method = "getLatestBlockhash",
            Params = [new { commitment }]
        };

    public static RpcRequest GetBalance(PublicKey account, Commitment commitment) =>
        new()
        {
            Method = "getBalance",
            Params = [account, new { commitment }]
        };

    public static RpcRequest GetSlot(Commitment commitment) =>
        new()
        {
            Method = "getSlot",
            Params = [new { commitment }]
        };

    public static RpcRequest GetHealth() =>
        new()
        {
            Method = "getHealth"
        };

    public static RpcRequest GetVersion() =>
        new()
        {
            Method = "getVersion"
        };

    public static RpcRequest GetBlockHeight(Commitment commitment) =>
        new()
        {
            Method = "getBlockHeight",
            Params = [new { commitment }]
        };

    public static RpcRequest GetTransactionCount(Commitment commitment) =>
        new()
        {
            Method = "getTransactionCount",
            Params = [new { commitment }]
        };

    public static RpcRequest GetTokenAccountBalance(PublicKey account, Commitment commitment) =>
        new()
        {
            Method = "getTokenAccountBalance",
            Params = [account, new { commitment }]
        };

    public static RpcRequest GetTokenSupply(PublicKey mint, Commitment commitment) =>
        new()
        {
            Method = "getTokenSupply",
            Params = [mint, new { commitment }]
        };

    public static RpcRequest GetMinimumBalanceForRentExemption(long dataLength, Commitment commitment) =>
        new()
        {
            Method = "getMinimumBalanceForRentExemption",
            Params = [dataLength, new { commitment }]
        };
}
