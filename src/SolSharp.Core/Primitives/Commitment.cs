using System.Text.Json.Serialization;
using SolSharp.Core.Converters;

namespace SolSharp.Core.Primitives;

/// <summary>
/// How finalized the bank state a request reads against must be. Serialized to the lowercase
/// wire strings Solana's JSON-RPC expects via <see cref="CommitmentJsonConverter"/>.
/// </summary>
[JsonConverter(typeof(CommitmentJsonConverter))]
public enum Commitment
{
    /// <summary>Block voted on by a supermajority of the cluster (optimistic confirmation).</summary>
    Confirmed,

    /// <summary>Block confirmed by a supermajority as having reached maximum lockout (finalized).</summary>
    Finalized,

    /// <summary>Most recent block; may still be skipped by the cluster.</summary>
    Processed
}
