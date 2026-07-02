using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Converters;

/// <summary>
/// Maps <see cref="Commitment"/> to and from the lowercase wire strings Solana's JSON-RPC uses.
/// Applied as a type attribute so the mapping holds regardless of the active JsonSerializerOptions.
/// Public because a source-generated <c>JsonSerializerContext</c> can only materialize a
/// converter-attributed type when it can construct the converter (SYSLIB1220 otherwise), and consumers
/// register models carrying <see cref="Commitment"/> properties in their own contexts.
/// </summary>
public sealed class CommitmentJsonConverter : JsonConverter<Commitment>
{
    /// <inheritdoc/>
    public override Commitment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "confirmed" => Commitment.Confirmed,
            "finalized" => Commitment.Finalized,
            "processed" => Commitment.Processed,
            var other => throw new JsonException($"Unknown commitment value: '{other}'.")
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Commitment value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            Commitment.Confirmed => "confirmed",
            Commitment.Finalized => "finalized",
            Commitment.Processed => "processed",
            _ => throw new JsonException($"Unknown commitment value: '{value}'.")
        });
}
