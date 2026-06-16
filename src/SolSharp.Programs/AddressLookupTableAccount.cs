using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// An on-chain Address Lookup Table as a v0 message consumes it: the table account's address and the
/// ordered addresses it stores. <see cref="MessageV0.Compile"/> moves referenced accounts found here out
/// of the static keys and into a table lookup.
/// </summary>
/// <param name="Key">The lookup table account's address.</param>
/// <param name="Addresses">The addresses stored in the table, in index order.</param>
public sealed record AddressLookupTableAccount(PublicKey Key, IReadOnlyList<PublicKey> Addresses);
