using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for the SPL Memo program: attaches a UTF-8 memo to a transaction, optionally signed.</summary>
public static class MemoProgram
{
    /// <summary>The SPL Memo program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse("MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr");

    /// <summary>
    /// Builds a memo instruction whose data is <paramref name="text"/> encoded as UTF-8. Any
    /// <paramref name="signers"/> must sign the transaction and are recorded with the memo on-chain.
    /// </summary>
    /// <param name="text">The memo text.</param>
    /// <param name="signers">The accounts that must sign the memo; pass none for an unsigned memo.</param>
    /// <returns>The memo instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="signers"/> is <c>null</c>.</exception>
    public static Instruction Memo(string text, params PublicKey[] signers)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(signers);

        var accounts = new AccountMeta[signers.Length];
        for (var i = 0; i < signers.Length; i++)
            accounts[i] = AccountMeta.WritableSigner(signers[i]);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = System.Text.Encoding.UTF8.GetBytes(text)
        };
    }
}
