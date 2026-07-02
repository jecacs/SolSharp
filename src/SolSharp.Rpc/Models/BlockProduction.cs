using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>Recent block production information, as returned by <c>getBlockProduction</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblockproduction">getBlockProduction</seealso>
public sealed record BlockProduction
{
    /// <summary>
    /// Production per validator identity (base58): a two-element list of the number of leader slots
    /// followed by the number of blocks actually produced.
    /// </summary>
    [JsonPropertyName("byIdentity")]
    public IReadOnlyDictionary<string, IReadOnlyList<ulong>> ByIdentity { get; init; } =
        new Dictionary<string, IReadOnlyList<ulong>>();

    /// <summary>The slot range the production information covers.</summary>
    [JsonPropertyName("range")]
    public BlockProductionRange Range { get; init; } = new();
}

/// <summary>The slot range covered by a <see cref="BlockProduction"/> result.</summary>
public sealed record BlockProductionRange
{
    /// <summary>The first slot of the range (inclusive).</summary>
    [JsonPropertyName("firstSlot")]
    public ulong FirstSlot { get; init; }

    /// <summary>The last slot of the range (inclusive).</summary>
    [JsonPropertyName("lastSlot")]
    public ulong LastSlot { get; init; }
}
