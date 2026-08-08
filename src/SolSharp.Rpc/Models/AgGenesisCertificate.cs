using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>
/// The Alpenglow genesis block certificate returned by <c>getAgGenesisCert</c> when the cluster has
/// switched to Alpenglow consensus.
/// </summary>
public sealed record AgGenesisCertificate
{
    /// <summary>The block certified as the Alpenglow genesis block.</summary>
    [JsonPropertyName("block")]
    public AgGenesisBlock Block { get; init; } = new();

    /// <summary>The aggregate BLS signature and validator-participation bitmap.</summary>
    [JsonPropertyName("signature")]
    public AgGenesisCertificateSignature Signature { get; init; } = new();
}

/// <summary>The block identified by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisBlock
{
    /// <summary>The block's slot.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The raw 32-byte block identifier.</summary>
    [JsonPropertyName("block_id")]
    public IReadOnlyList<byte> BlockId { get; init; } = [];
}

/// <summary>The signature carried by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisCertificateSignature
{
    /// <summary>The raw 192-byte aggregate BLS signature in affine-point representation.</summary>
    [JsonPropertyName("signature")]
    public IReadOnlyList<byte> Signature { get; init; } = [];

    /// <summary>A bitmap whose set bits identify the validator ranks included in the aggregate signature.</summary>
    [JsonPropertyName("bitmap")]
    public IReadOnlyList<byte> Bitmap { get; init; } = [];
}
