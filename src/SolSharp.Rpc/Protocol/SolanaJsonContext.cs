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
// HTTP response envelopes - one closed RpcResponse<T> per read shape the client requests.
[JsonSerializable(typeof(RpcResponse<RpcContextValue<LatestBlockhash>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<ulong>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<ulong?>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<bool>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<TokenAmount>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<SimulateTransactionResult>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<AccountInfo>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<AccountInfo?[]>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<ProgramAccount[]>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<SignatureStatus?[]>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<Supply>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<TokenLargestAccount[]>>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<ParsedAccountInfo>>))]
[JsonSerializable(typeof(RpcResponse<ulong>))]
[JsonSerializable(typeof(RpcResponse<string>))]
[JsonSerializable(typeof(RpcResponse<RpcVersion>))]
[JsonSerializable(typeof(RpcResponse<EpochInfo>))]
[JsonSerializable(typeof(RpcResponse<SignatureInfo[]>))]
[JsonSerializable(typeof(RpcResponse<ProgramAccount[]>))]
[JsonSerializable(typeof(RpcResponse<PrioritizationFee[]>))]
[JsonSerializable(typeof(RpcResponse<TransactionResponse>))]
[JsonSerializable(typeof(RpcResponse<PublicKey[]>))]
[JsonSerializable(typeof(RpcResponse<Block>))]
[JsonSerializable(typeof(RpcResponse<ParsedTransaction>))]
[JsonSerializable(typeof(RpcResponse<ParsedBlock>))]
[JsonSerializable(typeof(RpcResponse<VoteAccounts>))]
[JsonSerializable(typeof(RpcResponse<IReadOnlyList<InflationReward?>>))]
[JsonSerializable(typeof(RpcResponse<IReadOnlyDictionary<string, IReadOnlyList<int>>>))]
[JsonSerializable(typeof(RpcResponse<IReadOnlyList<ulong>>))]
[JsonSerializable(typeof(RpcResponse<IReadOnlyList<ClusterNode>>))]
[JsonSerializable(typeof(RpcResponse<BlockCommitment>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<BlockProduction>>))]
[JsonSerializable(typeof(RpcResponse<long?>))]
[JsonSerializable(typeof(RpcResponse<EpochSchedule>))]
[JsonSerializable(typeof(RpcResponse<HighestSnapshotSlot>))]
[JsonSerializable(typeof(RpcResponse<NodeIdentity>))]
[JsonSerializable(typeof(RpcResponse<InflationGovernor>))]
[JsonSerializable(typeof(RpcResponse<InflationRate>))]
[JsonSerializable(typeof(RpcResponse<RpcContextValue<LargestAccount[]>>))]
[JsonSerializable(typeof(RpcResponse<PerformanceSample[]>))]
[JsonSerializable(typeof(RpcResponse<PublicKey>))]
// WebSocket notification payloads (SubscriptionSink roots).
[JsonSerializable(typeof(SlotInfo))]
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
