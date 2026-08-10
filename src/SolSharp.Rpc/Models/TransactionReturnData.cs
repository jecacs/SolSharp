using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>Data returned by a program through Solana's transaction return-data syscall.</summary>
public sealed record TransactionReturnData
{
    /// <summary>The program that set the return data.</summary>
    [JsonPropertyName("programId")]
    [JsonRequired]
    public PublicKey ProgramId { get; init; }

    /// <summary>The returned bytes, decoded from the node's <c>[data, "base64"]</c> tuple.</summary>
    [JsonPropertyName("data")]
    [JsonConverter(typeof(Base64TupleJsonConverter))]
    [JsonRequired]
    public byte[] Data
    {
        get => field!;
        init => field = value ?? throw new JsonException("Transaction return data must carry non-null bytes.");
    }
}
