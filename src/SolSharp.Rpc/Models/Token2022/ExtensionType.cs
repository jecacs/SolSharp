namespace SolSharp.Rpc.Models.Token2022;

/// <summary>
/// A Token-2022 extension type - the <c>u16</c> tag of a TLV entry in an extended mint or token account.
/// Mint extensions appear only on mints, account extensions only on token accounts. Values mirror
/// <c>spl_token_2022_interface::extension::ExtensionType</c>.
/// </summary>
public enum ExtensionType : ushort
{
    /// <summary>Padding; also marks the end of the TLV data.</summary>
    Uninitialized = 0,

    /// <summary>Mint: the transfer-fee rate and the authorities that set and withdraw it.</summary>
    TransferFeeConfig = 1,

    /// <summary>Account: transfer fees withheld on this account.</summary>
    TransferFeeAmount = 2,

    /// <summary>Mint: an optional authority allowed to close the mint.</summary>
    MintCloseAuthority = 3,

    /// <summary>Mint: auditor configuration for confidential transfers.</summary>
    ConfidentialTransferMint = 4,

    /// <summary>Account: confidential-transfer state.</summary>
    ConfidentialTransferAccount = 5,

    /// <summary>Mint: the default state (e.g. frozen) for new token accounts.</summary>
    DefaultAccountState = 6,

    /// <summary>Account: the owner authority cannot be changed.</summary>
    ImmutableOwner = 7,

    /// <summary>Account: inbound transfers must carry a memo.</summary>
    MemoTransfer = 8,

    /// <summary>Mint: tokens of this mint cannot be transferred.</summary>
    NonTransferable = 9,

    /// <summary>Mint: tokens accrue interest over time.</summary>
    InterestBearingConfig = 10,

    /// <summary>Account: privileged token operations are locked from CPI.</summary>
    CpiGuard = 11,

    /// <summary>Mint: an optional permanent delegate over every account of the mint.</summary>
    PermanentDelegate = 12,

    /// <summary>Account: belongs to a non-transferable mint.</summary>
    NonTransferableAccount = 13,

    /// <summary>Mint: transfers must CPI into a program implementing the transfer-hook interface.</summary>
    TransferHook = 14,

    /// <summary>Account: belongs to a mint with a transfer hook.</summary>
    TransferHookAccount = 15,

    /// <summary>Mint: encrypted withheld fees and their encryption key.</summary>
    ConfidentialTransferFeeConfig = 16,

    /// <summary>Account: confidential withheld transfer fees.</summary>
    ConfidentialTransferFeeAmount = 17,

    /// <summary>Mint: a pointer to the account (possibly the mint itself) that holds token metadata.</summary>
    MetadataPointer = 18,

    /// <summary>Mint: token metadata stored in the mint itself.</summary>
    TokenMetadata = 19,

    /// <summary>Mint: a pointer to the account that holds group configurations.</summary>
    GroupPointer = 20,

    /// <summary>Mint: token-group configurations stored in the mint itself.</summary>
    TokenGroup = 21,

    /// <summary>Mint: a pointer to the account that holds group-member configurations.</summary>
    GroupMemberPointer = 22,

    /// <summary>Mint: token-group-member configurations stored in the mint itself.</summary>
    TokenGroupMember = 23,

    /// <summary>Mint: allows minting and burning of confidential tokens.</summary>
    ConfidentialMintBurn = 24,

    /// <summary>Mint: UI amounts are scaled by a configured multiplier.</summary>
    ScaledUiAmount = 25,

    /// <summary>Mint: minting, burning, and transferring can be paused.</summary>
    Pausable = 26,

    /// <summary>Account: belongs to a pausable mint.</summary>
    PausableAccount = 27,

    /// <summary>Mint: burning requires approval from an authority.</summary>
    PermissionedBurn = 28
}
