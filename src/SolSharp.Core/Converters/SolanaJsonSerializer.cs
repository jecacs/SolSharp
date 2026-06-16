using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Core.Converters;

/// <summary>
/// The shared <see cref="JsonSerializerOptions"/> SolSharp uses for every Solana JSON-RPC payload.
/// The wire mappings live on the types themselves (via <see cref="JsonConverterAttribute"/>), so these
/// options only add case-insensitive matching - resilience to provider casing - and drop nulls when
/// writing requests. The instance is frozen on creation, so it is safe to share across threads.
/// </summary>
public static class SolanaJsonSerializer
{
    /// <summary>The shared, read-only serializer options for Solana JSON-RPC payloads.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
