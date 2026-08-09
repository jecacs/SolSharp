using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// The <c>{ identity }</c> envelope <c>getIdentity</c> wraps its result in; the client unwraps it and
/// returns the <see cref="PublicKey"/> directly.
/// </summary>
internal sealed record NodeIdentity
{
    [JsonPropertyName("identity")]
    [JsonRequired]
    public PublicKey Identity { get; init; }
}
