using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// The transaction-level execution configuration embedded in a version-1 message. Every value is optional
/// because the V1 wire mask controls which settings are present.
/// </summary>
/// <seealso href="https://github.com/anza-xyz/agave/blob/master/transaction-status-client-types/src/lib.rs">
/// Agave's <c>UiTransactionConfig</c> JSON contract.
/// </seealso>
public sealed record ParsedTransactionConfig
{
    /// <summary>The total priority fee, in lamports; distinct from the legacy micro-lamports-per-CU price.</summary>
    [JsonPropertyName("priorityFee")]
    [JsonRequired]
    public ulong? PriorityFee { get; init; }

    /// <summary>The requested compute-unit limit.</summary>
    [JsonPropertyName("computeUnitLimit")]
    [JsonRequired]
    public uint? ComputeUnitLimit { get; init; }

    /// <summary>The requested loaded-account-data size limit, in bytes.</summary>
    [JsonPropertyName("loadedAccountsDataSizeLimit")]
    [JsonRequired]
    public uint? LoadedAccountsDataSizeLimit { get; init; }

    /// <summary>The requested program heap size, in bytes.</summary>
    [JsonPropertyName("heapSize")]
    [JsonRequired]
    public uint? HeapSize { get; init; }
}
