using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Protocol;

/// <summary>Builds the JSON-RPC request for each supported method.</summary>
/// <remarks>
/// Positional parameters are object-typed, and the source-generated <see cref="SolanaJsonContext"/>
/// dispatches them by their exact runtime type - so caller-supplied collections are materialized to
/// arrays here rather than passed through as arbitrary <see cref="IReadOnlyList{T}"/> implementations.
/// </remarks>
internal static class RpcRequests
{
    public static RpcRequest GetLatestBlockhash(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetLatestBlockhash,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetBalance(PublicKey account, Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetBalance,
            Params = [account, new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetSlot(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetSlot,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetHealth() =>
        new() { Method = RpcMethods.GetHealth };

    public static RpcRequest GetVersion() =>
        new() { Method = RpcMethods.GetVersion };

    public static RpcRequest GetBlockHeight(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetBlockHeight,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetTransactionCount(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetTransactionCount,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetTokenAccountBalance(PublicKey account, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenAccountBalance, Params = [account, new CommitmentConfig { Commitment = commitment }] };

    public static RpcRequest GetTokenSupply(PublicKey mint, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenSupply, Params = [mint, new CommitmentConfig { Commitment = commitment }] };

    public static RpcRequest GetMinimumBalanceForRentExemption(long dataLength, Commitment commitment) =>
        new() { Method = RpcMethods.GetMinimumBalanceForRentExemption, Params = [dataLength, new CommitmentConfig { Commitment = commitment }] };

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
                new SendTransactionConfig
                {
                    Encoding = "base64",
                    SkipPreflight = skipPreflight,
                    PreflightCommitment = preflightCommitment,
                    MaxRetries = maxRetries,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest SimulateTransaction(
        string base64Transaction,
        bool sigVerify,
        bool replaceRecentBlockhash,
        Commitment? commitment,
        ulong? minContextSlot,
        IReadOnlyList<PublicKey>? accounts,
        RpcAccountEncoding accountsEncoding,
        bool innerInstructions) =>
        new()
        {
            Method = RpcMethods.SimulateTransaction,
            Params =
            [
                base64Transaction,
                new SimulateTransactionConfig
                {
                    Encoding = "base64",
                    SigVerify = sigVerify,
                    ReplaceRecentBlockhash = replaceRecentBlockhash,
                    Commitment = commitment,
                    MinContextSlot = minContextSlot,
                    Accounts = accounts is null
                        ? null
                        : new SimulateTransactionAccountsConfig
                        {
                            Encoding = RpcWireNames.AccountEncoding(accountsEncoding),
                            Addresses = [.. accounts]
                        },
                    InnerInstructions = innerInstructions ? true : null
                }
            ]
        };

    public static RpcRequest GetAccountInfo(
        PublicKey account,
        Commitment? commitment,
        DataSlice? dataSlice = null,
        ulong? minContextSlot = null,
        RpcAccountEncoding? encoding = RpcAccountEncoding.Base64) =>
        new()
        {
            Method = RpcMethods.GetAccountInfo,
            Params =
            [
                account,
                new AccountInfoConfig
                {
                    Encoding = encoding is { } value ? RpcWireNames.AccountEncoding(value) : null,
                    Commitment = commitment,
                    DataSlice = dataSlice,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest GetMultipleAccounts(
        IReadOnlyList<PublicKey> accounts,
        Commitment? commitment,
        DataSlice? dataSlice = null,
        ulong? minContextSlot = null,
        RpcAccountEncoding? encoding = RpcAccountEncoding.Base64)
        => new()
        {
            Method = RpcMethods.GetMultipleAccounts,
            Params =
            [
                accounts.ToArray(),
                new AccountInfoConfig
                {
                    Encoding = encoding is { } value ? RpcWireNames.AccountEncoding(value) : null,
                    Commitment = commitment,
                    DataSlice = dataSlice,
                    MinContextSlot = minContextSlot
                }
            ]
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
                new SignaturesForAddressConfig
                {
                    Limit = limit,
                    Before = before,
                    Until = until,
                    Commitment = commitment,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest GetProgramAccounts(
        PublicKey programId,
        Commitment? commitment,
        IReadOnlyList<AccountFilter>? filters,
        DataSlice? dataSlice,
        ulong? minContextSlot,
        bool? withContext,
        bool? sortResults,
        RpcAccountEncoding? encoding = RpcAccountEncoding.Base64) =>
        new()
        {
            Method = RpcMethods.GetProgramAccounts,
            Params =
            [
                programId,
                new ProgramAccountsConfig
                {
                    Encoding = encoding is { } value ? RpcWireNames.AccountEncoding(value) : null,
                    Commitment = commitment,
                    MinContextSlot = minContextSlot,
                    DataSlice = dataSlice,
                    Filters = filters?.Select(filter => filter.Payload).ToArray(),
                    WithContext = withContext,
                    SortResults = sortResults
                }
            ]
        };

    public static RpcRequest GetEpochInfo(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetEpochInfo,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest IsBlockhashValid(string blockhash, Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.IsBlockhashValid,
            Params = [blockhash, new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetFeeForMessage(string base64Message, Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetFeeForMessage,
            Params = [base64Message, new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest RequestAirdrop(
        PublicKey account,
        ulong lamports,
        Commitment? commitment,
        string? recentBlockhash = null) =>
        new()
        {
            Method = RpcMethods.RequestAirdrop,
            Params =
            [
                account,
                lamports,
                new RequestAirdropConfig { RecentBlockhash = recentBlockhash, Commitment = commitment }
            ]
        };

    public static RpcRequest GetTokenAccountsByOwner(
        PublicKey owner,
        TokenAccountsFilter filter,
        Commitment? commitment,
        DataSlice? dataSlice = null,
        ulong? minContextSlot = null,
        RpcAccountEncoding? encoding = RpcAccountEncoding.Base64) =>
        new()
        {
            Method = RpcMethods.GetTokenAccountsByOwner,
            Params =
            [
                owner,
                TokenAccountsFilterPayload(filter),
                new AccountInfoConfig
                {
                    Encoding = encoding is { } value ? RpcWireNames.AccountEncoding(value) : null,
                    Commitment = commitment,
                    DataSlice = dataSlice,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest GetRecentPrioritizationFees(IReadOnlyList<PublicKey> accounts) =>
        new() { Method = RpcMethods.GetRecentPrioritizationFees, Params = [accounts.ToArray()] };

    public static RpcRequest GetTransaction(
        string signature,
        Commitment? commitment,
        byte? maxSupportedTransactionVersion,
        RpcTransactionEncoding? encoding = RpcTransactionEncoding.Base64) =>
        new()
        {
            Method = RpcMethods.GetTransaction,
            Params =
            [
                signature,
                new TransactionConfig
                {
                    Commitment = commitment,
                    MaxSupportedTransactionVersion = maxSupportedTransactionVersion,
                    Encoding = encoding is { } value ? RpcWireNames.TransactionEncoding(value) : null
                }
            ]
        };

    public static RpcRequest GetSignatureStatuses(IReadOnlyList<string> signatures, bool searchTransactionHistory) =>
        new()
        {
            Method = RpcMethods.GetSignatureStatuses,
            Params = [signatures.ToArray(), new SignatureStatusesConfig { SearchTransactionHistory = searchTransactionHistory }]
        };

    public static RpcRequest GetSlotLeaders(ulong startSlot, ulong limit) =>
        new() { Method = RpcMethods.GetSlotLeaders, Params = [startSlot, limit] };

    public static RpcRequest GetAgGenesisCert() =>
        new() { Method = RpcMethods.GetAgGenesisCert };

    public static RpcRequest GetSupply(Commitment? commitment, bool excludeNonCirculatingAccountsList) =>
        new()
        {
            Method = RpcMethods.GetSupply,
            Params =
            [
                new SupplyConfig
                {
                    Commitment = commitment,
                    ExcludeNonCirculatingAccountsList = excludeNonCirculatingAccountsList
                }
            ]
        };

    public static RpcRequest GetTokenLargestAccounts(PublicKey mint, Commitment commitment) =>
        new() { Method = RpcMethods.GetTokenLargestAccounts, Params = [mint, new CommitmentConfig { Commitment = commitment }] };

    public static RpcRequest GetBlock(
        ulong slot,
        Commitment? commitment,
        byte? maxSupportedTransactionVersion,
        RpcTransactionEncoding? encoding = null,
        RpcTransactionDetails? transactionDetails = RpcTransactionDetails.Signatures,
        bool? rewards = false) =>
        new()
        {
            Method = RpcMethods.GetBlock,
            Params =
            [
                slot,
                new BlockConfig
                {
                    Commitment = commitment,
                    MaxSupportedTransactionVersion = maxSupportedTransactionVersion,
                    Encoding = encoding is { } encodingValue
                        ? RpcWireNames.TransactionEncoding(encodingValue)
                        : null,
                    TransactionDetails = transactionDetails is { } detailsValue
                        ? RpcWireNames.TransactionDetails(detailsValue)
                        : null,
                    Rewards = rewards
                }
            ]
        };

    public static RpcRequest GetParsedTransaction(
        string signature,
        Commitment commitment,
        byte maxSupportedTransactionVersion) =>
        new()
        {
            Method = RpcMethods.GetTransaction,
            Params =
            [
                signature,
                new TransactionConfig
                {
                    Commitment = commitment,
                    MaxSupportedTransactionVersion = maxSupportedTransactionVersion,
                    Encoding = "jsonParsed"
                }
            ]
        };

    public static RpcRequest GetParsedBlock(
        ulong slot,
        Commitment commitment,
        byte maxSupportedTransactionVersion) =>
        new()
        {
            Method = RpcMethods.GetBlock,
            Params =
            [
                slot,
                new BlockConfig
                {
                    Commitment = commitment,
                    MaxSupportedTransactionVersion = maxSupportedTransactionVersion,
                    Encoding = "jsonParsed",
                    TransactionDetails = "full",
                    Rewards = false
                }
            ]
        };

    public static RpcRequest GetVoteAccounts(
        Commitment? commitment,
        PublicKey? votePublicKey = null,
        bool? keepUnstakedDelinquents = null,
        ulong? delinquentSlotDistance = null) =>
        new()
        {
            Method = RpcMethods.GetVoteAccounts,
            Params =
            [
                new VoteAccountsConfig
                {
                    VotePublicKey = votePublicKey,
                    Commitment = commitment,
                    KeepUnstakedDelinquents = keepUnstakedDelinquents,
                    DelinquentSlotDistance = delinquentSlotDistance
                }
            ]
        };

    public static RpcRequest GetInflationReward(
        IReadOnlyList<PublicKey> addresses,
        ulong? epoch,
        Commitment? commitment,
        ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetInflationReward,
            Params =
            [
                addresses.ToArray(),
                new InflationRewardConfig
                {
                    Commitment = commitment,
                    Epoch = epoch,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest GetLeaderSchedule(ulong? slot, Commitment? commitment, PublicKey? identity = null)
    {
        // The slot stays in position 0 even when absent: the node expects a u64-or-null there, so a bare
        // [config] would be misread as the slot. null! puts a literal JSON null without the nullable warning.
        object[] parameters = slot is { } s
            ? [s, new LeaderScheduleConfig { Identity = identity, Commitment = commitment }]
            : [null!, new LeaderScheduleConfig { Identity = identity, Commitment = commitment }];

        return new RpcRequest { Method = RpcMethods.GetLeaderSchedule, Params = parameters };
    }

    public static RpcRequest GetBlocks(
        ulong startSlot,
        ulong? endSlot,
        Commitment? commitment,
        ulong? minContextSlot = null)
    {
        object[] parameters = endSlot is { } end
            ? [startSlot, end, new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
            : [startSlot, new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }];

        return new RpcRequest { Method = RpcMethods.GetBlocks, Params = parameters };
    }

    public static RpcRequest GetClusterNodes() =>
        new() { Method = RpcMethods.GetClusterNodes };

    public static RpcRequest GetParsedAccountInfo(
        PublicKey account,
        Commitment? commitment,
        ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetAccountInfo,
            Params =
            [
                account,
                new AccountInfoConfig
                {
                    Encoding = "jsonParsed",
                    Commitment = commitment,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    public static RpcRequest GetBlockCommitment(ulong slot) =>
        new() { Method = RpcMethods.GetBlockCommitment, Params = [slot] };

    public static RpcRequest GetBlockProduction(Commitment commitment, PublicKey? identity, ulong? firstSlot, ulong? lastSlot) =>
        new()
        {
            Method = RpcMethods.GetBlockProduction,
            Params =
            [
                new BlockProductionConfig
                {
                    Commitment = commitment,
                    Identity = identity,
                    Range = firstSlot is { } first ? new SlotRangeConfig { FirstSlot = first, LastSlot = lastSlot } : null
                }
            ]
        };

    public static RpcRequest GetBlockTime(ulong slot) =>
        new() { Method = RpcMethods.GetBlockTime, Params = [slot] };

    public static RpcRequest GetBlocksWithLimit(
        ulong startSlot,
        ulong limit,
        Commitment? commitment,
        ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetBlocksWithLimit,
            Params =
            [
                startSlot,
                limit,
                new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }
            ]
        };

    public static RpcRequest GetEpochSchedule() =>
        new() { Method = RpcMethods.GetEpochSchedule };

    public static RpcRequest GetFirstAvailableBlock() =>
        new() { Method = RpcMethods.GetFirstAvailableBlock };

    public static RpcRequest GetGenesisHash() =>
        new() { Method = RpcMethods.GetGenesisHash };

    public static RpcRequest GetHighestSnapshotSlot() =>
        new() { Method = RpcMethods.GetHighestSnapshotSlot };

    public static RpcRequest GetIdentity() =>
        new() { Method = RpcMethods.GetIdentity };

    public static RpcRequest GetInflationGovernor(Commitment commitment) =>
        new() { Method = RpcMethods.GetInflationGovernor, Params = [new CommitmentConfig { Commitment = commitment }] };

    public static RpcRequest GetInflationRate() =>
        new() { Method = RpcMethods.GetInflationRate };

    public static RpcRequest GetLargestAccounts(
        Commitment? commitment,
        LargestAccountsFilter? filter,
        bool? sortResults = null) =>
        new()
        {
            Method = RpcMethods.GetLargestAccounts,
            Params =
            [
                new LargestAccountsConfig
                {
                    Commitment = commitment,
                    Filter = filter switch
                    {
                        LargestAccountsFilter.Circulating => "circulating",
                        LargestAccountsFilter.NonCirculating => "nonCirculating",
                        _ => null
                    },
                    SortResults = sortResults
                }
            ]
        };

    public static RpcRequest GetMaxRetransmitSlot() =>
        new() { Method = RpcMethods.GetMaxRetransmitSlot };

    public static RpcRequest GetMaxShredInsertSlot() =>
        new() { Method = RpcMethods.GetMaxShredInsertSlot };

    public static RpcRequest GetRecentPerformanceSamples(int? limit) =>
        new() { Method = RpcMethods.GetRecentPerformanceSamples, Params = limit is { } value ? [value] : [] };

    public static RpcRequest GetSlotLeader(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetSlotLeader,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetStakeMinimumDelegation(Commitment? commitment, ulong? minContextSlot = null) =>
        new()
        {
            Method = RpcMethods.GetStakeMinimumDelegation,
            Params = [new ContextConfig { Commitment = commitment, MinContextSlot = minContextSlot }]
        };

    public static RpcRequest GetTokenAccountsByDelegate(
        PublicKey delegateAccount,
        TokenAccountsFilter filter,
        Commitment? commitment,
        DataSlice? dataSlice = null,
        ulong? minContextSlot = null,
        RpcAccountEncoding? encoding = RpcAccountEncoding.Base64) =>
        new()
        {
            Method = RpcMethods.GetTokenAccountsByDelegate,
            Params =
            [
                delegateAccount,
                TokenAccountsFilterPayload(filter),
                new AccountInfoConfig
                {
                    Encoding = encoding is { } value ? RpcWireNames.AccountEncoding(value) : null,
                    Commitment = commitment,
                    DataSlice = dataSlice,
                    MinContextSlot = minContextSlot
                }
            ]
        };

    private static object TokenAccountsFilterPayload(TokenAccountsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return filter.Kind switch
        {
            TokenAccountsFilterKind.Mint => new MintFilter { Mint = filter.Address },
            TokenAccountsFilterKind.ProgramId => new ProgramIdFilter { ProgramId = filter.Address },
            _ => throw new ArgumentOutOfRangeException(nameof(filter), "Unknown token-account filter kind.")
        };
    }

    public static RpcRequest MinimumLedgerSlot() =>
        new() { Method = RpcMethods.MinimumLedgerSlot };
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
    public const string GetAgGenesisCert = "getAgGenesisCert";
    public const string GetSupply = "getSupply";
    public const string GetTokenLargestAccounts = "getTokenLargestAccounts";
    public const string GetBlock = "getBlock";
    public const string GetVoteAccounts = "getVoteAccounts";
    public const string GetInflationReward = "getInflationReward";
    public const string GetLeaderSchedule = "getLeaderSchedule";
    public const string GetBlocks = "getBlocks";
    public const string GetClusterNodes = "getClusterNodes";
    public const string GetBlockCommitment = "getBlockCommitment";
    public const string GetBlockProduction = "getBlockProduction";
    public const string GetBlockTime = "getBlockTime";
    public const string GetBlocksWithLimit = "getBlocksWithLimit";
    public const string GetEpochSchedule = "getEpochSchedule";
    public const string GetFirstAvailableBlock = "getFirstAvailableBlock";
    public const string GetGenesisHash = "getGenesisHash";
    public const string GetHighestSnapshotSlot = "getHighestSnapshotSlot";
    public const string GetIdentity = "getIdentity";
    public const string GetInflationGovernor = "getInflationGovernor";
    public const string GetInflationRate = "getInflationRate";
    public const string GetLargestAccounts = "getLargestAccounts";
    public const string GetMaxRetransmitSlot = "getMaxRetransmitSlot";
    public const string GetMaxShredInsertSlot = "getMaxShredInsertSlot";
    public const string GetRecentPerformanceSamples = "getRecentPerformanceSamples";
    public const string GetSlotLeader = "getSlotLeader";
    public const string GetStakeMinimumDelegation = "getStakeMinimumDelegation";
    public const string GetTokenAccountsByDelegate = "getTokenAccountsByDelegate";
    public const string MinimumLedgerSlot = "minimumLedgerSlot";
}
