namespace SolSharp.Programs;

/// <summary>
/// A Token-2022 extension type as encoded in account-size and reallocation instructions. Values mirror the
/// pinned <c>spl_token_2022_interface::extension::ExtensionType</c> wire tags.
/// </summary>
public enum Token2022ExtensionType : ushort
{
    /// <summary>Padding or an uninitialized extension entry.</summary>
    Uninitialized = 0,

    /// <summary>Mint transfer-fee configuration.</summary>
    TransferFeeConfig = 1,

    /// <summary>Account withheld transfer-fee amount.</summary>
    TransferFeeAmount = 2,

    /// <summary>Mint close authority.</summary>
    MintCloseAuthority = 3,

    /// <summary>Confidential-transfer mint configuration.</summary>
    ConfidentialTransferMint = 4,

    /// <summary>Confidential-transfer account state.</summary>
    ConfidentialTransferAccount = 5,

    /// <summary>Default state for newly initialized token accounts.</summary>
    DefaultAccountState = 6,

    /// <summary>Immutable token-account owner marker.</summary>
    ImmutableOwner = 7,

    /// <summary>Required incoming-transfer memo state.</summary>
    MemoTransfer = 8,

    /// <summary>Non-transferable mint marker.</summary>
    NonTransferable = 9,

    /// <summary>Interest-bearing mint configuration.</summary>
    InterestBearingConfig = 10,

    /// <summary>Token-account CPI guard state.</summary>
    CpiGuard = 11,

    /// <summary>Mint permanent delegate.</summary>
    PermanentDelegate = 12,

    /// <summary>Non-transferable token-account marker.</summary>
    NonTransferableAccount = 13,

    /// <summary>Mint transfer-hook configuration.</summary>
    TransferHook = 14,

    /// <summary>Token-account transfer-hook state.</summary>
    TransferHookAccount = 15,

    /// <summary>Confidential transfer-fee mint configuration.</summary>
    ConfidentialTransferFeeConfig = 16,

    /// <summary>Confidential withheld-fee account state.</summary>
    ConfidentialTransferFeeAmount = 17,

    /// <summary>Mint metadata pointer.</summary>
    MetadataPointer = 18,

    /// <summary>In-mint token metadata.</summary>
    TokenMetadata = 19,

    /// <summary>Mint group pointer.</summary>
    GroupPointer = 20,

    /// <summary>In-mint token-group configuration.</summary>
    TokenGroup = 21,

    /// <summary>Mint group-member pointer.</summary>
    GroupMemberPointer = 22,

    /// <summary>In-mint token-group-member configuration.</summary>
    TokenGroupMember = 23,

    /// <summary>Confidential mint and burn configuration.</summary>
    ConfidentialMintBurn = 24,

    /// <summary>Scaled UI amount configuration.</summary>
    ScaledUiAmount = 25,

    /// <summary>Pausable mint configuration.</summary>
    Pausable = 26,

    /// <summary>Pausable token-account marker.</summary>
    PausableAccount = 27,

    /// <summary>Permissioned-burn mint configuration.</summary>
    PermissionedBurn = 28
}
