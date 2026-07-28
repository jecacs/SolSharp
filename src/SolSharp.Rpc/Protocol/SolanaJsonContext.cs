using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Protocol;

/// <summary>
/// The source-generated JSON metadata for every type SolSharp puts on or reads off the wire - resolved
/// by <see cref="RpcJson.Options"/> (chained with <c>CoreJsonContext</c>), which keeps the whole
/// RPC/WebSocket surface reflection-free and Native AOT compatible. Positional request parameters are
/// object-typed and dispatch by exact runtime type, so every boxed parameter shape must be registered;
/// a new client method whose root type is missing fails loudly with <see cref="NotSupportedException"/>
/// (the offline client test suite exercises each method through these options and catches that).
/// Registering Core's converter-attributed primitives here works only because their converters are
/// public - an inaccessible converter makes the generator drop the type (SYSLIB1220/SYSLIB1030).
/// </summary>
// Request envelope and boxed positional parameters.
[JsonSerializable(typeof(RpcRequest))]
[JsonSerializable(typeof(IReadOnlyList<RpcRequest>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(PublicKey))]
[JsonSerializable(typeof(PublicKey[]))]
[JsonSerializable(typeof(string[]))]
// Request configuration objects (see RpcParams.cs and AccountFilter.cs).
[JsonSerializable(typeof(CommitmentConfig))]
[JsonSerializable(typeof(SendTransactionConfig))]
[JsonSerializable(typeof(SimulateTransactionConfig))]
[JsonSerializable(typeof(AccountInfoConfig))]
[JsonSerializable(typeof(SignaturesForAddressConfig))]
[JsonSerializable(typeof(ProgramAccountsConfig))]
[JsonSerializable(typeof(MintFilter))]
[JsonSerializable(typeof(TransactionConfig))]
[JsonSerializable(typeof(BlockConfig))]
[JsonSerializable(typeof(SupplyConfig))]
[JsonSerializable(typeof(SignatureStatusesConfig))]
[JsonSerializable(typeof(InflationRewardConfig))]
[JsonSerializable(typeof(LogsFilter))]
[JsonSerializable(typeof(BlockSubscribeFilter))]
[JsonSerializable(typeof(BlockSubscribeConfig))]
[JsonSerializable(typeof(MemcmpFilter))]
[JsonSerializable(typeof(DataSizeFilter))]
[JsonSerializable(typeof(LargestAccountsConfig))]
[JsonSerializable(typeof(BlockProductionConfig))]
// HTTP response envelopes - the response envelope and every result shape requested by the client.
[JsonSerializable(typeof(RpcResponse))]
[JsonSerializable(typeof(RpcContextValue<ulong?>))]
[JsonSerializable(typeof(RpcContextValue<bool>))]
[JsonSerializable(typeof(RpcContextValue<SimulateTransactionResult>))]
[JsonSerializable(typeof(RpcContextValue<AccountInfo?[]>))]
[JsonSerializable(typeof(RpcContextValue<ProgramAccount[]>))]
[JsonSerializable(typeof(RpcContextValue<SignatureStatus?[]>))]
[JsonSerializable(typeof(RpcContextValue<Supply>))]
[JsonSerializable(typeof(RpcContextValue<TokenLargestAccount[]>))]
[JsonSerializable(typeof(RpcVersion))]
[JsonSerializable(typeof(EpochInfo))]
[JsonSerializable(typeof(SignatureInfo[]))]
[JsonSerializable(typeof(ProgramAccount[]))]
[JsonSerializable(typeof(PrioritizationFee[]))]
[JsonSerializable(typeof(TransactionResponse))]
[JsonSerializable(typeof(Block))]
[JsonSerializable(typeof(ParsedTransaction))]
[JsonSerializable(typeof(ParsedBlock))]
[JsonSerializable(typeof(VoteAccounts))]
[JsonSerializable(typeof(IReadOnlyList<InflationReward?>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, IReadOnlyList<int>>))]
[JsonSerializable(typeof(IReadOnlyList<ulong>))]
[JsonSerializable(typeof(IReadOnlyList<ClusterNode>))]
[JsonSerializable(typeof(BlockCommitment))]
[JsonSerializable(typeof(RpcContextValue<BlockProduction>))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(EpochSchedule))]
[JsonSerializable(typeof(HighestSnapshotSlot))]
[JsonSerializable(typeof(NodeIdentity))]
[JsonSerializable(typeof(InflationGovernor))]
[JsonSerializable(typeof(InflationRate))]
[JsonSerializable(typeof(RpcContextValue<LargestAccount[]>))]
[JsonSerializable(typeof(PerformanceSample[]))]
// WebSocket notification payloads (SubscriptionSink roots).
[JsonSerializable(typeof(SlotInfo))]
[JsonSerializable(typeof(VoteNotification))]
[JsonSerializable(typeof(SlotsUpdate))]
[JsonSerializable(typeof(RpcContextValue<LogInfo>))]
[JsonSerializable(typeof(RpcContextValue<AccountInfo>))]
[JsonSerializable(typeof(RpcContextValue<ParsedAccountInfo>))]
[JsonSerializable(typeof(RpcContextValue<ProgramAccount>))]
[JsonSerializable(typeof(RpcContextValue<BlockNotification>))]
[JsonSerializable(typeof(RpcContextValue<ParsedBlockNotification>))]
[JsonSerializable(typeof(RpcContextValue<SignatureNotification>))]
// Batch result values (RpcBatch map delegates deserialize these directly).
[JsonSerializable(typeof(RpcContextValue<ulong>))]
[JsonSerializable(typeof(RpcContextValue<LatestBlockhash>))]
[JsonSerializable(typeof(RpcContextValue<TokenAmount>))]
// Types reached only from inside hand-written converters: a [JsonConverter]-attributed type is opaque to
// the generator's graph walk, so what its converter deserializes must be registered explicitly.
[JsonSerializable(typeof(ParsedMessage))]
[JsonSerializable(typeof(ParsedTransactionMeta))]
[JsonSerializable(typeof(ParsedInstructionInfo))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SolanaJsonContext : JsonSerializerContext;
