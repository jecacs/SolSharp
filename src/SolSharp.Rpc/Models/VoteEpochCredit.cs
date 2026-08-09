using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>One <c>[epoch, credits, previousCredits]</c> tuple from a vote account RPC response.</summary>
[JsonConverter(typeof(VoteEpochCreditJsonConverter))]
public readonly record struct VoteEpochCredit
{
    /// <summary>Creates one exact vote-credit tuple.</summary>
    /// <param name="epoch">The epoch that earned the credits.</param>
    /// <param name="credits">Cumulative credits at the end of the epoch.</param>
    /// <param name="previousCredits">Cumulative credits at the beginning of the epoch.</param>
    public VoteEpochCredit(ulong epoch, ulong credits, ulong previousCredits)
    {
        Epoch = epoch;
        Credits = credits;
        PreviousCredits = previousCredits;
    }

    /// <summary>The epoch that earned the credits.</summary>
    public ulong Epoch { get; }

    /// <summary>Cumulative credits at the end of the epoch.</summary>
    public ulong Credits { get; }

    /// <summary>Cumulative credits at the beginning of the epoch.</summary>
    public ulong PreviousCredits { get; }
}

/// <summary>Reads and writes the exact three-element vote-credit tuple used by Agave.</summary>
public sealed class VoteEpochCreditJsonConverter : JsonConverter<VoteEpochCredit>
{
    /// <inheritdoc/>
    public override VoteEpochCredit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var epoch) ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var credits) ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var previousCredits) ||
            !reader.Read() || reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("A vote epoch-credit value must be exactly [epoch, credits, previousCredits] as u64 values.");
        }

        return new VoteEpochCredit(epoch, credits, previousCredits);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, VoteEpochCredit value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Epoch);
        writer.WriteNumberValue(value.Credits);
        writer.WriteNumberValue(value.PreviousCredits);
        writer.WriteEndArray();
    }
}
