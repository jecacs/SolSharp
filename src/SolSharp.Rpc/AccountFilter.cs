namespace SolSharp.Rpc;

/// <summary>
/// A <c>getProgramAccounts</c> / <c>programSubscribe</c> filter. Build one with <see cref="MemoryCompare"/>
/// (a <c>memcmp</c> match at an offset) or <see cref="DataSize"/> (an exact data-length match); an account
/// must satisfy every supplied filter to be returned.
/// </summary>
public sealed class AccountFilter
{
    private AccountFilter(object payload) => Payload = payload;

    internal object Payload { get; }

    /// <summary>Matches accounts whose data at <paramref name="offset"/> equals <paramref name="bytesBase58"/> (a <c>memcmp</c> filter).</summary>
    /// <param name="offset">The byte offset into the account data to compare from.</param>
    /// <param name="bytesBase58">The bytes to match, base58-encoded.</param>
    /// <returns>The filter.</returns>
    public static AccountFilter MemoryCompare(int offset, string bytesBase58) =>
        new(new { memcmp = new { offset, bytes = bytesBase58, encoding = "base58" } });

    /// <summary>Matches accounts whose data is exactly <paramref name="size"/> bytes long (a <c>dataSize</c> filter).</summary>
    /// <param name="size">The required account data length in bytes.</param>
    /// <returns>The filter.</returns>
    public static AccountFilter DataSize(long size) =>
        new(new { dataSize = size });
}
