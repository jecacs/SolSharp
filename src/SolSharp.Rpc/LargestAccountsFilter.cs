namespace SolSharp.Rpc;

/// <summary>Narrows a <c>getLargestAccounts</c> query to one side of the circulating-supply split.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getlargestaccounts">getLargestAccounts</seealso>
public enum LargestAccountsFilter
{
    /// <summary>Only accounts that are part of the circulating supply.</summary>
    Circulating,

    /// <summary>Only accounts that are excluded from the circulating supply.</summary>
    NonCirculating
}
