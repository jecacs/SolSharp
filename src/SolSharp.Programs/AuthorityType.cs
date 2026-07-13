namespace SolSharp.Programs;

/// <summary>
/// Which authority of a mint or token account <see cref="TokenProgram.SetAuthority"/> changes. The first
/// four variants are the classic SPL Token set; the rest are Token-2022 extension authorities, valid only
/// when the instruction targets the Token-2022 program.
/// </summary>
public enum AuthorityType : byte
{
    /// <summary>A mint's minting authority.</summary>
    MintTokens = 0,

    /// <summary>A mint's freeze authority.</summary>
    FreezeAccount = 1,

    /// <summary>A token account's owner.</summary>
    AccountOwner = 2,

    /// <summary>A token account's close authority.</summary>
    CloseAccount = 3,

    /// <summary>Token-2022: the authority allowed to set the mint's transfer fee.</summary>
    TransferFeeConfig = 4,

    /// <summary>Token-2022: the authority allowed to withdraw withheld (transfer-fee) tokens from the mint.</summary>
    WithheldWithdraw = 5,

    /// <summary>Token-2022: the authority allowed to close the mint account.</summary>
    CloseMint = 6,

    /// <summary>Token-2022: the authority allowed to set the interest rate.</summary>
    InterestRate = 7,

    /// <summary>Token-2022: the authority allowed to transfer or burn any tokens of the mint.</summary>
    PermanentDelegate = 8,

    /// <summary>Token-2022: the authority allowed to update the confidential-transfer mint and approve accounts for confidential transfers.</summary>
    ConfidentialTransferMint = 9,

    /// <summary>Token-2022: the authority allowed to set the transfer-hook program id.</summary>
    TransferHookProgramId = 10,

    /// <summary>Token-2022: the authority allowed to set the withdraw-withheld-authority encryption key.</summary>
    ConfidentialTransferFeeConfig = 11,

    /// <summary>Token-2022: the authority allowed to set the metadata address.</summary>
    MetadataPointer = 12,

    /// <summary>Token-2022: the authority allowed to set the group address.</summary>
    GroupPointer = 13,

    /// <summary>Token-2022: the authority allowed to set the group member address.</summary>
    GroupMemberPointer = 14,

    /// <summary>Token-2022: the authority allowed to set the UI amount scale.</summary>
    ScaledUiAmount = 15,

    /// <summary>Token-2022: the authority allowed to pause or resume minting, transferring, and burning.</summary>
    Pause = 16,

    /// <summary>Token-2022: the authority allowed to perform a permissioned token burn.</summary>
    PermissionedBurn = 17
}
