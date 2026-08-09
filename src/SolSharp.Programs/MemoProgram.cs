using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for the SPL Memo program: attaches a UTF-8 memo to a transaction, optionally signed.</summary>
public static class MemoProgram
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The SPL Memo program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse("MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr");

    /// <summary>
    /// Builds a memo instruction whose data is <paramref name="text"/> encoded as UTF-8. Any
    /// <paramref name="signers"/> must sign the transaction and are recorded with the memo on-chain;
    /// they are referenced as read-only signers, since the memo program writes to no account.
    /// </summary>
    /// <param name="text">The memo text.</param>
    /// <param name="signers">The accounts that must sign the memo; pass none for an unsigned memo.</param>
    /// <returns>The memo instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="signers"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> contains invalid Unicode text.</exception>
    public static Instruction Memo(string text, params PublicKey[] signers)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(signers);

        byte[] data;
        try
        {
            data = StrictUtf8.GetBytes(text);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("A memo must contain valid Unicode text.", nameof(text), exception);
        }

        return Memo(data, signers);
    }

    /// <summary>
    /// Builds a memo instruction from raw bytes. The Memo program validates UTF-8 on-chain; accepting bytes here
    /// preserves exact client-side parity with the Rust builder and permits callers to handle validation timing.
    /// </summary>
    /// <param name="memo">The exact instruction bytes.</param>
    /// <param name="signers">The read-only accounts that must sign the memo.</param>
    /// <returns>The memo instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> is <c>null</c>.</exception>
    public static Instruction Memo(ReadOnlySpan<byte> memo, params PublicKey[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        // Read-only signers, matching the Rust spl-memo builder (AccountMeta::new_readonly(pubkey, true));
        // a writable flag would needlessly write-lock the signer accounts. (solana-py marks them writable.)
        var accounts = new AccountMeta[signers.Length];
        for (var i = 0; i < signers.Length; i++)
            accounts[i] = AccountMeta.ReadonlySigner(signers[i]);

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = memo.ToArray()
        };
    }
}
