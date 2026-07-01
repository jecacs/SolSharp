namespace SolSharp.Programs;

/// <summary>Which authority of a mint or token account <see cref="TokenProgram.SetAuthority"/> changes.</summary>
public enum AuthorityType : byte
{
    /// <summary>A mint's minting authority.</summary>
    MintTokens = 0,

    /// <summary>A mint's freeze authority.</summary>
    FreezeAccount = 1,

    /// <summary>A token account's owner.</summary>
    AccountOwner = 2,

    /// <summary>A token account's close authority.</summary>
    CloseAccount = 3
}
