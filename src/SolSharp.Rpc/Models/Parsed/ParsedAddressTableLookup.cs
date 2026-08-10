using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>An address lookup-table reference embedded in a parsed versioned message.</summary>
public sealed record ParsedAddressTableLookup
{
    /// <summary>The address lookup-table account.</summary>
    [JsonPropertyName("accountKey")]
    [JsonRequired]
    public PublicKey AccountKey { get; init; }

    /// <summary>Indexes of writable addresses loaded from the table.</summary>
    [JsonPropertyName("writableIndexes")]
    [JsonRequired]
    public IReadOnlyList<byte> WritableIndexes
    {
        get => field!;
        init => field = value ?? throw new JsonException("An address-table lookup must carry writable indexes.");
    }

    /// <summary>Indexes of read-only addresses loaded from the table.</summary>
    [JsonPropertyName("readonlyIndexes")]
    [JsonRequired]
    public IReadOnlyList<byte> ReadonlyIndexes
    {
        get => field!;
        init => field = value ?? throw new JsonException("An address-table lookup must carry read-only indexes.");
    }
}
