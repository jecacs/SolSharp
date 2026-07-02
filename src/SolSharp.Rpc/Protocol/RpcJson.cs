using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SolSharp.Core.Converters;

namespace SolSharp.Rpc.Protocol;

/// <summary>
/// The frozen <see cref="JsonSerializerOptions"/> every RPC and WebSocket path serializes through: the
/// same wire conventions as <c>SolanaJsonSerializer.Options</c> (case-insensitive reads, nulls dropped on
/// writes), resolved exclusively by source-generated contexts - no reflection, Native AOT safe. The
/// resolver chains <see cref="SolanaJsonContext"/> with <see cref="CoreJsonContext"/> so any Core wire
/// primitive resolves even where it is only reached through runtime dispatch. A type missing from both
/// fails fast with <see cref="NotSupportedException"/>.
/// </summary>
internal static class RpcJson
{
    /// <summary>The shared, frozen options instance; safe to use concurrently from any thread.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Returns the source-generated metadata for <typeparamref name="T"/>, bound to <see cref="Options"/>.</summary>
    /// <typeparam name="T">The registered root type to resolve.</typeparam>
    /// <returns>The type's metadata.</returns>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not registered in either context.</exception>
    public static JsonTypeInfo<T> TypeInfo<T>() => Options.GetTypeInfo<T>();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(SolanaJsonContext.Default, CoreJsonContext.Default)
        };

        options.MakeReadOnly();
        return options;
    }
}

/// <summary>Typed sugar over <see cref="JsonSerializerOptions.GetTypeInfo"/>.</summary>
internal static class JsonOptionsExtensions
{
    /// <summary>
    /// Returns the metadata for <typeparamref name="T"/> from whatever resolver backs
    /// <paramref name="options"/> - the AOT-safe lookup converters use for their nested reads, so they
    /// keep working under any caller-supplied options as well as <see cref="RpcJson.Options"/>.
    /// </summary>
    /// <typeparam name="T">The type to resolve.</typeparam>
    /// <param name="options">The active serializer options.</param>
    /// <returns>The type's metadata.</returns>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not resolvable from <paramref name="options"/>.</exception>
    public static JsonTypeInfo<T> GetTypeInfo<T>(this JsonSerializerOptions options)
        => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
