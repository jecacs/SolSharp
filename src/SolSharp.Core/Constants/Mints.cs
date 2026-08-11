namespace SolSharp.Core.Constants;

/// <summary>Well-known SPL token mint addresses on mainnet (base58).</summary>
public static class Mints
{
    /// <summary>The wrapped SOL (wSOL) mint owned by the classic SPL Token program.</summary>
    public const string WrappedSol = "So11111111111111111111111111111111111111112";

    /// <summary>
    /// The Token-2022 native mint, a program-derived address of the seeds <c>"native-mint"</c> and
    /// <c>255</c> under the Token-2022 program. It is a distinct account from <see cref="WrappedSol"/>;
    /// Token-2022 native-SOL accounts wrap this mint, not the classic one.
    /// </summary>
    public const string Token2022NativeMint = "9pan9bMn5HatX4EJdBwg9VgCa7Uz5HL8N1m5D3NdXejP";

    /// <summary>The USDC (Circle) mint.</summary>
    public const string Usdc = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    /// <summary>The USDT (Tether) mint.</summary>
    public const string Usdt = "Es9vMFrzaCERmJfrF4H2FYD4KCoNkY11McCe8BenwNYB";
}
