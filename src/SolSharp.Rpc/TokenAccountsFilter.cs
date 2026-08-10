using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

internal enum TokenAccountsFilterKind
{
    /// <summary>A mint-address filter.</summary>
    Mint,

    /// <summary>An SPL Token program-id filter.</summary>
    ProgramId
}

/// <summary>
/// The mutually exclusive mint or token-program filter accepted by
/// <c>getTokenAccountsByOwner</c> and <c>getTokenAccountsByDelegate</c>.
/// </summary>
public sealed class TokenAccountsFilter
{
    private TokenAccountsFilter(TokenAccountsFilterKind kind, PublicKey address)
    {
        Kind = kind;
        Address = address;
    }

    internal TokenAccountsFilterKind Kind { get; }

    internal PublicKey Address { get; }

    /// <summary>Matches token accounts for one mint.</summary>
    /// <param name="mint">The mint to match.</param>
    /// <returns>A mint filter.</returns>
    public static TokenAccountsFilter ByMint(PublicKey mint) =>
        new(TokenAccountsFilterKind.Mint, mint);

    /// <summary>Matches every account owned by one SPL Token program, such as Token or Token-2022.</summary>
    /// <param name="programId">The SPL Token program id to match.</param>
    /// <returns>A token-program filter.</returns>
    public static TokenAccountsFilter ByProgramId(PublicKey programId) =>
        new(TokenAccountsFilterKind.ProgramId, programId);
}
