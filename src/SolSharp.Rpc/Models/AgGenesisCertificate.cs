using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>
/// The Alpenglow genesis block certificate returned by <c>getAgGenesisCert</c> when the cluster has
/// switched to Alpenglow consensus.
/// </summary>
public sealed record AgGenesisCertificate
{
    private AgGenesisBlock? _block;
    private AgGenesisCertificateSignature? _signature;

    /// <summary>The block certified as the Alpenglow genesis block.</summary>
    [JsonPropertyName("block")]
    public required AgGenesisBlock Block
    {
        get => _block ?? throw new InvalidOperationException("The Alpenglow genesis block has not been initialized.");
        init => _block = value ?? throw new JsonException("An Alpenglow genesis certificate must carry a block.");
    }

    /// <summary>The aggregate BLS signature and validator-participation bitmap.</summary>
    [JsonPropertyName("signature")]
    public required AgGenesisCertificateSignature Signature
    {
        get => _signature ?? throw new InvalidOperationException("The Alpenglow genesis signature has not been initialized.");
        init => _signature = value ?? throw new JsonException("An Alpenglow genesis certificate must carry a signature.");
    }
}

/// <summary>The block identified by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisBlock
{
    private IReadOnlyList<byte>? _blockId;

    /// <summary>The block's slot.</summary>
    [JsonPropertyName("slot")]
    public required ulong Slot { get; init; }

    /// <summary>The raw 32-byte block identifier.</summary>
    [JsonPropertyName("block_id")]
    public required IReadOnlyList<byte> BlockId
    {
        get => _blockId ?? throw new InvalidOperationException("The Alpenglow block identifier has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow block identifier cannot be null.");
            if (value.Count != 32)
                throw new JsonException("An Alpenglow block identifier must contain exactly 32 bytes.");

            _blockId = Array.AsReadOnly(value.ToArray());
        }
    }
}

/// <summary>The signature carried by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisCertificateSignature
{
    private IReadOnlyList<byte>? _signature;
    private IReadOnlyList<byte>? _bitmap;

    /// <summary>The raw 192-byte aggregate BLS signature in affine-point representation.</summary>
    [JsonPropertyName("signature")]
    public required IReadOnlyList<byte> Signature
    {
        get => _signature ?? throw new InvalidOperationException("The Alpenglow aggregate signature has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow aggregate signature cannot be null.");
            if (value.Count != 192)
                throw new JsonException("An Alpenglow aggregate signature must contain exactly 192 bytes.");

            _signature = Array.AsReadOnly(value.ToArray());
        }
    }

    /// <summary>
    /// A bitmap whose set bits identify the validator ranks included in the aggregate signature;
    /// the pinned certificate format supports at most 4,096 validators (512 bytes).
    /// </summary>
    [JsonPropertyName("bitmap")]
    public required IReadOnlyList<byte> Bitmap
    {
        get => _bitmap ?? throw new InvalidOperationException("The Alpenglow validator bitmap has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow validator bitmap cannot be null.");
            if (value.Count > 512)
                throw new JsonException("An Alpenglow validator bitmap cannot exceed 512 bytes.");
            _bitmap = Array.AsReadOnly(value.ToArray());
        }
    }
}
