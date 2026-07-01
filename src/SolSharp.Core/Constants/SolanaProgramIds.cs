namespace SolSharp.Core.Constants;

/// <summary>Well-known Solana program addresses (base58).</summary>
public static class SolanaProgramIds
{
    /// <summary>The System program: account creation, SOL transfers, and account ownership assignment.</summary>
    public const string SystemProgram = "11111111111111111111111111111111";

    /// <summary>The SPL Token program.</summary>
    public const string TokenProgram = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    /// <summary>The SPL Token-2022 (Token Extensions) program.</summary>
    public const string Token2022Program = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";

    /// <summary>The Associated Token Account (ATA) program.</summary>
    public const string AssociatedTokenProgram = "ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL";

    /// <summary>The Compute Budget program: compute-unit limits and priority fees.</summary>
    public const string ComputeBudgetProgram = "ComputeBudget111111111111111111111111111111";

    /// <summary>The Address Lookup Table program.</summary>
    public const string AddressLookupTableProgram = "AddressLookupTab1e1111111111111111111111111";

    /// <summary>The SPL Memo program.</summary>
    public const string MemoProgram = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";

    /// <summary>The Metaplex Token Metadata program.</summary>
    public const string TokenMetadataProgram = "metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s";

    /// <summary>The OpenBook (formerly Serum) central-limit-order-book DEX program.</summary>
    public const string OpenBookDex = "srmqPvymJeFKQ4zGQed1GFppgkRHL9kaELCbyksJtPX";

    /// <summary>Raydium's Liquidity Pool V4 (AMM) program.</summary>
    public const string RaydiumLiquidityV4 = "675kPX9MHTjS2zt1qfr1NYHuzeLXfQM9H24wFSUt1Mp8";

    /// <summary>Raydium's constant-product market maker (CPMM) program.</summary>
    public const string RaydiumCpmm = "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

    /// <summary>Raydium's LaunchLab token-launch program.</summary>
    public const string RaydiumLaunchLab = "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj";

    /// <summary>The pump.fun bonding-curve token-launch program.</summary>
    public const string PumpFun = "6EF8rrecthR5Dkzon8Nwu78hRvfCKubJ14M5uBEwF6P";

    /// <summary>The PumpSwap AMM program.</summary>
    public const string PumpSwap = "pAMMBay6oceH9fJKBRHGP5D4bD4sWpmSwMn52FMfXEA";

    /// <summary>Meteora's Dynamic AMM v2 (DAMM v2) program.</summary>
    public const string MeteoraDammV2 = "cpamdpZCGKUy5JxQXB4dcpGPiikHawvSWAd6mEn1sGG";

    /// <summary>The Jupiter swap-aggregator v6 program.</summary>
    public const string JupiterAggregatorV6 = "JUP6LkbZbjS1jKKwapdHNy74zcZ3tLUZoi5QNyVTaV4";

    /// <summary>The Squads multisig program.</summary>
    public const string Squads = "SQDS4ep65T869zMMBKyuUq6aD6EgTu8psMjkvj52pCf";

    /// <summary>Raydium's LP-token lock program.</summary>
    public const string RaydiumLpLock = "LockrWmn6K5twhz3y9w1dQERbmgSaRkfnTeTKbpofwE";
}
