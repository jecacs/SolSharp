using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Core.Converters;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> preconfigured with SolSharp's Solana wire conventions:
/// case-insensitive property matching on reads (resilience to provider casing) and null dropping on
/// writes. The instance is frozen over a source-generated resolver covering the Core wire primitives
/// (<see cref="SolSharp.Core.Primitives.Commitment"/>, <see cref="SolSharp.Core.Primitives.Hash"/>,
/// <see cref="SolSharp.Core.Primitives.PublicKey"/>),
/// so it never falls back to reflection and stays Native AOT compatible; serializing any other type with
/// it throws <see cref="NotSupportedException"/>. For custom models, create your own options - the wire
/// mappings live on the types themselves (via <see cref="JsonConverterAttribute"/>), so they hold their
/// wire form under any options.
/// </summary>
public static class SolanaJsonSerializer
{
    /// <summary>The shared, frozen options instance; safe to use concurrently from any thread.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = CoreJsonContext.Default
        };

        options.MakeReadOnly();
        return options;
    }
}
