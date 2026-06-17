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

    public static RpcRequest SendTransaction(
        string base64Transaction,
        bool skipPreflight,
        Commitment? preflightCommitment,
        uint? maxRetries,
        ulong? minContextSlot) =>
        new()
        {
            Method = "sendTransaction",
            Params =
            [
                base64Transaction,
                new
                {
                    encoding = "base64",
                    skipPreflight,
                    preflightCommitment,
                    maxRetries,
                    minContextSlot
                }
            ]
        };

    public static RpcRequest SimulateTransaction(
        string base64Transaction,
        bool sigVerify,
        bool replaceRecentBlockhash,
        Commitment? commitment,
        ulong? minContextSlot) =>
        new()
        {
            Method = "simulateTransaction",
            Params =
            [
                base64Transaction,
                new
                {
                    encoding = "base64",
                    sigVerify,
                    replaceRecentBlockhash,
                    commitment,
                    minContextSlot
                }
            ]
        };

    public static RpcRequest GetAccountInfo(PublicKey account, Commitment commitment) =>
        new()
        {
            Method = "getAccountInfo",
            Params = [account, new { encoding = "base64", commitment }]
        };

    public static RpcRequest GetMultipleAccounts(IReadOnlyList<PublicKey> accounts, Commitment commitment) =>
        new()
        {
            Method = "getMultipleAccounts",
            Params = [accounts, new { encoding = "base64", commitment }]
        };

    public static RpcRequest GetSignaturesForAddress(
        PublicKey address,
        int? limit,
        string? before,
        string? until,
        Commitment? commitment,
        ulong? minContextSlot) =>
        new()
        {
            Method = "getSignaturesForAddress",
            Params =
            [
                address,
                new { limit, before, until, commitment, minContextSlot }
            ]
        };

    public static RpcRequest GetProgramAccounts(
        PublicKey programId,
        Commitment? commitment,
        IReadOnlyList<AccountFilter>? filters,
        ulong? minContextSlot) =>
        new()
        {
            Method = "getProgramAccounts",
            Params =
            [
                programId,
                new
                {
                    encoding = "base64",
                    commitment,
                    minContextSlot,
                    filters = filters?.Select(filter => filter.Payload).ToArray()
                }
            ]
        };

    public static RpcRequest GetEpochInfo(Commitment commitment) =>
        new()
        {
            Method = "getEpochInfo",
            Params = [new { commitment }]
        };

    public static RpcRequest IsBlockhashValid(string blockhash, Commitment commitment) =>
        new()
        {
            Method = "isBlockhashValid",
            Params = [blockhash, new { commitment }]
        };

    public static RpcRequest GetFeeForMessage(string base64Message, Commitment commitment) =>
        new()
        {
            Method = "getFeeForMessage",
            Params = [base64Message, new { commitment }]
        };

    public static RpcRequest RequestAirdrop(PublicKey account, ulong lamports, Commitment commitment) =>
        new()
        {
            Method = "requestAirdrop",
            Params = [account, lamports, new { commitment }]
        };

    public static RpcRequest GetTokenAccountsByOwner(PublicKey owner, PublicKey mint, Commitment commitment) =>
        new()
        {
            Method = "getTokenAccountsByOwner",
            Params = [owner, new { mint }, new { encoding = "base64", commitment }]
        };

    public static RpcRequest GetRecentPrioritizationFees(IReadOnlyList<PublicKey> accounts) =>
        new()
        {
            Method = "getRecentPrioritizationFees",
            Params = [accounts]
        };

    public static RpcRequest GetTransaction(string signature, Commitment commitment) =>
        new()
        {
            Method = "getTransaction",
            Params =
            [
                signature,
                new { commitment, maxSupportedTransactionVersion = 0, encoding = "base64" }
            ]
        };

    public static RpcRequest GetSignatureStatuses(IReadOnlyList<string> signatures, bool searchTransactionHistory) =>
        new()
        {
            Method = "getSignatureStatuses",
            Params = [signatures, new { searchTransactionHistory }]
        };

    public static RpcRequest GetSlotLeaders(ulong startSlot, ulong limit) =>
        new()
        {
            Method = "getSlotLeaders",
            Params = [startSlot, limit]
        };

    public static RpcRequest GetSupply(Commitment commitment) =>
        new()
        {
            Method = "getSupply",
            Params = [new { commitment, excludeNonCirculatingAccountsList = true }]
        };

    public static RpcRequest GetTokenLargestAccounts(PublicKey mint, Commitment commitment) =>
        new()
        {
            Method = "getTokenLargestAccounts",
            Params = [mint, new { commitment }]
        };

    public static RpcRequest GetBlock(ulong slot, Commitment commitment) =>
        new()
        {
            Method = "getBlock",
            Params =
            [
                slot,
                new { commitment, maxSupportedTransactionVersion = 0, transactionDetails = "signatures", rewards = false }
            ]
        };
}
