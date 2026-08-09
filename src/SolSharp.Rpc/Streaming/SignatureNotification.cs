using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A <c>signatureSubscribe</c> notification: normally the final processed result, with an optional earlier
/// received event when requested. The node unsubscribes automatically after the final result.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/signaturesubscribe">signatureSubscribe</seealso>
[JsonConverter(typeof(SignatureNotificationJsonConverter))]
public sealed record SignatureNotification
{
    /// <summary>Whether this is the early received event or the final processed result.</summary>
    public SignatureNotificationKind Kind { get; init; }

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    public JsonElement? Err { get; init; }

    /// <summary>True for the optional early <c>"receivedSignature"</c> event.</summary>
    [JsonIgnore]
    public bool IsReceived => Kind == SignatureNotificationKind.Received;

    /// <summary>True for the final processed-signature object.</summary>
    [JsonIgnore]
    public bool IsFinal => Kind == SignatureNotificationKind.Processed;

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => IsFinal && Err is { ValueKind: not JsonValueKind.Null };
}

/// <summary>The two wire variants emitted by <c>signatureSubscribe</c>.</summary>
public enum SignatureNotificationKind
{
    /// <summary>The final <c>{ err }</c> processed-signature result.</summary>
    Processed,

    /// <summary>The early <c>"receivedSignature"</c> event.</summary>
    Received
}

/// <summary>Converts the untagged string-or-object signature notification union.</summary>
public sealed class SignatureNotificationJsonConverter : JsonConverter<SignatureNotification>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override SignatureNotification Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.String)
        {
            if (root.GetString() != "receivedSignature")
                throw new JsonException("Expected the receivedSignature event.");

            return new SignatureNotification { Kind = SignatureNotificationKind.Received };
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("err", out var error))
            throw new JsonException("Expected a receivedSignature string or a processed { err } object.");

        return new SignatureNotification
        {
            Kind = SignatureNotificationKind.Processed,
            Err = error.Clone()
        };
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        SignatureNotification value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (value.Kind)
        {
            case SignatureNotificationKind.Processed:
                writer.WriteStartObject();
                writer.WritePropertyName("err");
                if (value.Err is { } error)
                    error.WriteTo(writer);
                else
                    writer.WriteNullValue();
                writer.WriteEndObject();
                break;
            case SignatureNotificationKind.Received when value.Err is null:
                writer.WriteStringValue("receivedSignature");
                break;
            case SignatureNotificationKind.Received:
                throw new JsonException("A receivedSignature event cannot carry an error payload.");
            default:
                throw new JsonException($"Unknown signature notification kind '{value.Kind}'.");
        }
    }
}
