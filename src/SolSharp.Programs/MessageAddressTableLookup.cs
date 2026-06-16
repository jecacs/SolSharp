using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A compiled reference from a v0 message to an address lookup table: which of the table's addresses the
/// message loads, split into the writable and read-only accounts.
/// </summary>
public sealed class MessageAddressTableLookup
{
    /// <summary>The lookup table account's address.</summary>
    public required PublicKey AccountKey { get; init; }

    /// <summary>Indexes into the table of the writable accounts the message loads from it.</summary>
    public required byte[] WritableIndexes { get; init; }

    /// <summary>Indexes into the table of the read-only accounts the message loads from it.</summary>
    public required byte[] ReadonlyIndexes { get; init; }
}
