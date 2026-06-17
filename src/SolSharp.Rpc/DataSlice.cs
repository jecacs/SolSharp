namespace SolSharp.Rpc;

/// <summary>
/// A request to return only a slice of an account's data rather than the whole account - a bandwidth
/// optimization for large accounts when only a known region (a header, a discriminator) is needed.
/// </summary>
/// <param name="Offset">The byte offset into the account data to start at.</param>
/// <param name="Length">The number of bytes to return.</param>
public sealed record DataSlice(int Offset, int Length);
