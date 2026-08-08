using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>An address lookup-table reference embedded in a parsed versioned message.</summary>
public sealed record ParsedAddressTableLookup
{
    /// <summary>The address lookup-table account.</summary>
    [JsonPropertyName("accountKey")]
    public PublicKey AccountKey { get; init; }

    /// <summary>Indexes of writable addresses loaded from the table.</summary>
    [JsonPropertyName("writableIndexes")]
    public IReadOnlyList<byte> WritableIndexes { get; init; } = [];

    /// <summary>Indexes of read-only addresses loaded from the table.</summary>
    [JsonPropertyName("readonlyIndexes")]
    public IReadOnlyList<byte> ReadonlyIndexes { get; init; } = [];
}
