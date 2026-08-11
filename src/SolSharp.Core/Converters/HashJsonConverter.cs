using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Converters;

/// <summary>
/// Reads and writes <see cref="Hash"/> as the base58 string used for Solana blockhashes, durable
/// nonces, and message hashes. Public so source-generated consumer contexts can construct it.
/// </summary>
public sealed class HashJsonConverter : JsonConverter<Hash>
{
    /// <inheritdoc/>
    public override Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a hash string, got {reader.TokenType}.");

        // Reject oversized JSON tokens before GetString creates a potentially huge UTF-16 allocation.
        // A valid base58 character is one ASCII byte, or at most six bytes when written as \uXXXX.
        var rawLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        var maximumRawLength = reader.ValueIsEscaped ? Hash.MaxBase58Length * 6L : Hash.MaxBase58Length;
        if (rawLength > maximumRawLength)
            throw new JsonException("Invalid hash base58 value.");

        var text = reader.GetString();
        if (Hash.TryParse(text, out var hash))
            return hash;

        throw new JsonException("Invalid hash base58 value.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Hash value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
