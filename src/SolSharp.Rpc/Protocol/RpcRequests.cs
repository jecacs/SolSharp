using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Protocol;

/// <summary>Builds the JSON-RPC request for each supported method.</summary>
internal static class RpcRequests
{
    public static RpcRequest GetLatestBlockhash(Commitment commitment) =>
        new() { Method = RpcMethods.GetLatestBlockhash, Params = [new { commitment }] };

    public static RpcRequest GetBalance(PublicKey account, Commitment commitment) =>
        new() { Method = RpcMethods.GetBalance, Params = [account, new { commitment }] };

    public static RpcRequest GetSlot(Commitment commitment) =>
        new() { Method = RpcMethods.GetSlot, Params = [new { commitment }] };

    public static RpcRequest GetHealth() =>
        new() { Method = RpcMethods.GetHealth };

    public static RpcRequest GetVersion() =>
        new() { Method = RpcMethods.GetVersion };

    public static RpcRequest GetBlockHeight(Commitment commitment) =>
        new() { Method = RpcMethods.GetBlockHeight, Params = [new { commitment }] };

    public static RpcRequest GetTransactionCount(Commitment commitment) =>
        new() { Method = RpcMethods.GetTransactionCount, Params = [new { commitment }] };

    public static RpcRequest GetTokenAccountBalance(PublicKey account, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenAccountBalance, Params = [account, new { commitment }] };

    public static RpcRequest GetTokenSupply(PublicKey mint, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenSupply, Params = [mint, new { commitment }] };

    public static RpcRequest GetMinimumBalanceForRentExemption(long dataLength, Commitment commitment) =>
        new() { Method = RpcMethods.GetMinimumBalanceForRentExemption, Params = [dataLength, new { commitment }] };

    public static RpcRequest SendTransaction(
        string base64Transaction,
        bool skipPreflight,
        Commitment? preflightCommitment,
        uint? maxRetries,
        ulong? minContextSlot) =>
        new()
        {
            Method = RpcMethods.SendTransaction,
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
            Method = RpcMethods.SimulateTransaction,
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

    public static RpcRequest GetAccountInfo(PublicKey account, Commitment commitment, DataSlice? dataSlice = null) =>
        new()
        {
            Method = RpcMethods.GetAccountInfo,
            Params =
            [
                account,
                new
                {
                    encoding = "base64",
                    commitment,
                    dataSlice = dataSlice is { } slice ? new { offset = slice.Offset, length = slice.Length } : null
                }
            ]
        };

    public static RpcRequest GetMultipleAccounts(IReadOnlyList<PublicKey> accounts, Commitment commitment)
        => new()
        {
            Method = RpcMethods.GetMultipleAccounts,
            Params = [accounts, new
            {
                encoding = "base64",
                commitment
            }]
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
            Method = RpcMethods.GetSignaturesForAddress,
            Params =
            [
                address,
                new
                {
                    limit,
                    before,
                    until,
                    commitment,
                    minContextSlot
                }
            ]
        };

    public static RpcRequest GetProgramAccounts(
        PublicKey programId,
        Commitment? commitment,
        IReadOnlyList<AccountFilter>? filters,
        DataSlice? dataSlice,
        ulong? minContextSlot) =>
        new()
        {
            Method = RpcMethods.GetProgramAccounts,
            Params =
            [
                programId,
                new
                {
                    encoding = "base64",
                    commitment,
                    minContextSlot,
                    dataSlice =
                        dataSlice is { } slice ? new { offset = slice.Offset, length = slice.Length } : null,
                    filters = filters?.Select(filter => filter.Payload).ToArray()
                }
            ]
        };

    public static RpcRequest GetEpochInfo(Commitment commitment) =>
        new() { Method = RpcMethods.GetEpochInfo, Params = [new { commitment }] };

    public static RpcRequest IsBlockhashValid(string blockhash, Commitment commitment) =>
        new() { Method = RpcMethods.IsBlockhashValid, Params = [blockhash, new { commitment }] };

    public static RpcRequest GetFeeForMessage(string base64Message, Commitment commitment) =>
        new() { Method = RpcMethods.GetFeeForMessage, Params = [base64Message, new { commitment }] };

    public static RpcRequest RequestAirdrop(PublicKey account, ulong lamports, Commitment commitment) =>
        new() { Method = RpcMethods.RequestAirdrop, Params = [account, lamports, new { commitment }] };

    public static RpcRequest GetTokenAccountsByOwner(PublicKey owner, PublicKey mint, Commitment commitment) =>
        new()
        {
            Method = RpcMethods.GetTokenAccountsByOwner,
            Params = [owner, new { mint }, new { encoding = "base64", commitment }]
        };

    public static RpcRequest GetRecentPrioritizationFees(IReadOnlyList<PublicKey> accounts) =>
        new() { Method = RpcMethods.GetRecentPrioritizationFees, Params = [accounts] };

    public static RpcRequest GetTransaction(string signature, Commitment commitment) =>
        new()
        {
            Method = RpcMethods.GetTransaction,
            Params =
            [
                signature,
                new { commitment, maxSupportedTransactionVersion = 0, encoding = "base64" }
            ]
        };

    public static RpcRequest GetSignatureStatuses(IReadOnlyList<string> signatures, bool searchTransactionHistory) =>
        new() { Method = RpcMethods.GetSignatureStatuses, Params = [signatures, new { searchTransactionHistory }] };

    public static RpcRequest GetSlotLeaders(ulong startSlot, ulong limit) =>
        new() { Method = RpcMethods.GetSlotLeaders, Params = [startSlot, limit] };

    public static RpcRequest GetSupply(Commitment commitment) =>
        new() { Method = RpcMethods.GetSupply, Params = [new { commitment, excludeNonCirculatingAccountsList = true }] };

    public static RpcRequest GetTokenLargestAccounts(PublicKey mint, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenLargestAccounts, Params = [mint, new { commitment }] };

    public static RpcRequest GetBlock(ulong slot, Commitment commitment) =>
        new()
        {
            Method = RpcMethods.GetBlock,
            Params =
            [
                slot,
                new
                {
                    commitment,
                    maxSupportedTransactionVersion = 0,
                    transactionDetails = "signatures",
                    rewards = false
                }
            ]
        };

    public static RpcRequest GetParsedTransaction(string signature, Commitment commitment) =>
        new()
        {
            Method = RpcMethods.GetTransaction,
            Params =
            [
                signature,
                new { commitment, maxSupportedTransactionVersion = 0, encoding = "jsonParsed" }
            ]
        };

    public static RpcRequest GetParsedBlock(ulong slot, Commitment commitment) =>
        new()
        {
            Method = RpcMethods.GetBlock,
            Params =
            [
                slot,
                new
                {
                    commitment,
                    maxSupportedTransactionVersion = 0,
                    encoding = "jsonParsed",
                    transactionDetails = "full",
                    rewards = false
                }
            ]
        };

    public static RpcRequest GetVoteAccounts(Commitment commitment) =>
        new() { Method = RpcMethods.GetVoteAccounts, Params = [new { commitment }] };

    public static RpcRequest GetInflationReward(IReadOnlyList<PublicKey> addresses, ulong? epoch, Commitment commitment) =>
        new() { Method = RpcMethods.GetInflationReward, Params = [addresses, new { commitment, epoch }] };

    public static RpcRequest GetLeaderSchedule(ulong? slot, Commitment commitment)
    {
        // The slot stays in position 0 even when absent: the node expects a u64-or-null there, so a bare
        // [config] would be misread as the slot. null! puts a literal JSON null without the nullable warning.
        object[] parameters = slot is { } s
            ? [s, new { commitment }]
            : [null!, new { commitment }];

        return new RpcRequest { Method = RpcMethods.GetLeaderSchedule, Params = parameters };
    }

    public static RpcRequest GetBlocks(ulong startSlot, ulong? endSlot, Commitment commitment)
    {
        object[] parameters = endSlot is { } end
            ? [startSlot, end, new { commitment }]
            : [startSlot, new { commitment }];

        return new RpcRequest { Method = RpcMethods.GetBlocks, Params = parameters };
    }

    public static RpcRequest GetClusterNodes() =>
        new() { Method = RpcMethods.GetClusterNodes };

    public static RpcRequest GetParsedAccountInfo(PublicKey account, Commitment commitment) =>
        new() { Method = RpcMethods.GetAccountInfo, Params = [account, new { encoding = "jsonParsed", commitment }] };
}

internal static class RpcMethods
{
    public const string GetLatestBlockhash = "getLatestBlockhash";
    public const string GetBalance = "getBalance";
    public const string GetSlot = "getSlot";
    public const string GetHealth = "getHealth";
    public const string GetVersion = "getVersion";
    public const string GetBlockHeight = "getBlockHeight";
    public const string GetTransactionCount = "getTransactionCount";
    public const string GetTokenAccountBalance = "getTokenAccountBalance";
    public const string GetTokenSupply = "getTokenSupply";
    public const string GetMinimumBalanceForRentExemption = "getMinimumBalanceForRentExemption";
    public const string SendTransaction = "sendTransaction";
    public const string SimulateTransaction = "simulateTransaction";
    public const string GetAccountInfo = "getAccountInfo";
    public const string GetMultipleAccounts = "getMultipleAccounts";
    public const string GetSignaturesForAddress = "getSignaturesForAddress";
    public const string GetProgramAccounts = "getProgramAccounts";
    public const string GetEpochInfo = "getEpochInfo";
    public const string IsBlockhashValid = "isBlockhashValid";
    public const string GetFeeForMessage = "getFeeForMessage";
    public const string RequestAirdrop = "requestAirdrop";
    public const string GetTokenAccountsByOwner = "getTokenAccountsByOwner";
    public const string GetRecentPrioritizationFees = "getRecentPrioritizationFees";
    public const string GetTransaction = "getTransaction";
    public const string GetSignatureStatuses = "getSignatureStatuses";
    public const string GetSlotLeaders = "getSlotLeaders";
    public const string GetSupply = "getSupply";
    public const string GetTokenLargestAccounts = "getTokenLargestAccounts";
    public const string GetBlock = "getBlock";
    public const string GetVoteAccounts = "getVoteAccounts";
    public const string GetInflationReward = "getInflationReward";
    public const string GetLeaderSchedule = "getLeaderSchedule";
    public const string GetBlocks = "getBlocks";
    public const string GetClusterNodes = "getClusterNodes";
}
