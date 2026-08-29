using System.Text.Json;
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
    public required AgGenesisBlock Block
    {
        get => field ?? throw new InvalidOperationException("The Alpenglow genesis block has not been initialized.");
        init => field = value ?? throw new JsonException("An Alpenglow genesis certificate must carry a block.");
    }

    /// <summary>The aggregate BLS signature and validator-participation bitmap.</summary>
    [JsonPropertyName("signature")]
    public required AgGenesisCertificateSignature Signature
    {
        get => field ?? throw new InvalidOperationException("The Alpenglow genesis signature has not been initialized.");
        init => field = value ?? throw new JsonException("An Alpenglow genesis certificate must carry a signature.");
    }
}

/// <summary>The block identified by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisBlock
{
    /// <summary>The block's slot.</summary>
    [JsonPropertyName("slot")]
    public required ulong Slot { get; init; }

    /// <summary>The raw 32-byte block identifier.</summary>
    [JsonPropertyName("block_id")]
    public required IReadOnlyList<byte> BlockId
    {
        get => field ?? throw new InvalidOperationException("The Alpenglow block identifier has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow block identifier cannot be null.");
            if (value.Count != 32)
                throw new JsonException("An Alpenglow block identifier must contain exactly 32 bytes.");

            field = Array.AsReadOnly([.. value]);
        }
    }
}

/// <summary>The signature carried by an <see cref="AgGenesisCertificate"/>.</summary>
public sealed record AgGenesisCertificateSignature
{
    /// <summary>The raw 192-byte aggregate BLS signature in affine-point representation.</summary>
    [JsonPropertyName("signature")]
    public required IReadOnlyList<byte> Signature
    {
        get => field ?? throw new InvalidOperationException("The Alpenglow aggregate signature has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow aggregate signature cannot be null.");
            if (value.Count != 192)
                throw new JsonException("An Alpenglow aggregate signature must contain exactly 192 bytes.");

            field = Array.AsReadOnly([.. value]);
        }
    }

    /// <summary>
    /// A versioned Base2 signer-store blob whose bit payload identifies the participating validator
    /// ranks. Its one-byte version and two-byte bit-count envelope make the pinned 4,096-validator
    /// decoder limit 515 bytes.
    /// </summary>
    [JsonPropertyName("bitmap")]
    public required IReadOnlyList<byte> Bitmap
    {
        get => field ?? throw new InvalidOperationException("The Alpenglow validator bitmap has not been initialized.");
        init
        {
            if (value is null)
                throw new JsonException("An Alpenglow validator bitmap cannot be null.");
            if (value.Count > 515)
                throw new JsonException("An Alpenglow validator bitmap cannot exceed 515 bytes.");
            field = Array.AsReadOnly([.. value]);
        }
    }
}
