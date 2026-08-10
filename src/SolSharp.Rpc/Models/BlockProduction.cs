using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The exact <c>[leaderSlots, blocksProduced]</c> tuple for one validator identity.</summary>
[JsonConverter(typeof(BlockProductionCountsJsonConverter))]
public readonly record struct BlockProductionCounts
{
    /// <summary>Creates one exact block-production count tuple.</summary>
    /// <param name="leaderSlots">The number of slots assigned to the validator.</param>
    /// <param name="blocksProduced">The number of assigned slots in which it produced a block.</param>
    public BlockProductionCounts(ulong leaderSlots, ulong blocksProduced)
    {
        LeaderSlots = leaderSlots;
        BlocksProduced = blocksProduced;
    }

    /// <summary>The number of slots assigned to the validator.</summary>
    public ulong LeaderSlots { get; }

    /// <summary>The number of assigned slots in which the validator produced a block.</summary>
    public ulong BlocksProduced { get; }
}

/// <summary>Recent block production information, as returned by <c>getBlockProduction</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblockproduction">getBlockProduction</seealso>
public sealed record BlockProduction
{
    /// <summary>Production counts keyed by validator identity (base58).</summary>
    [JsonPropertyName("byIdentity")]
    [JsonRequired]
    public IReadOnlyDictionary<string, BlockProductionCounts> ByIdentity
    {
        get => field ?? throw new InvalidOperationException("Block-production identities have not been initialized.");
        init => field = value ?? throw new JsonException("Block production must carry its identity counts.");
    }

    /// <summary>The slot range the production information covers.</summary>
    [JsonPropertyName("range")]
    [JsonRequired]
    public BlockProductionRange Range
    {
        get => field ?? throw new InvalidOperationException("The block-production range has not been initialized.");
        init => field = value ?? throw new JsonException("Block production must carry its slot range.");
    }
}

/// <summary>Reads and writes the exact two-element block-production count tuple used by Agave.</summary>
public sealed class BlockProductionCountsJsonConverter : JsonConverter<BlockProductionCounts>
{
    /// <inheritdoc/>
    public override BlockProductionCounts Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var leaderSlots) ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var blocksProduced) ||
            !reader.Read() || reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("A block-production count must be exactly [leaderSlots, blocksProduced] as u64 values.");
        }

        return new(leaderSlots, blocksProduced);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, BlockProductionCounts value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.LeaderSlots);
        writer.WriteNumberValue(value.BlocksProduced);
        writer.WriteEndArray();
    }
}

/// <summary>The slot range covered by a <see cref="BlockProduction"/> result.</summary>
public sealed record BlockProductionRange
{
    /// <summary>The first slot of the range (inclusive).</summary>
    [JsonPropertyName("firstSlot")]
    [JsonRequired]
    public ulong FirstSlot { get; init; }

    /// <summary>The last slot of the range (inclusive).</summary>
    [JsonPropertyName("lastSlot")]
    [JsonRequired]
    public ulong LastSlot { get; init; }
}
