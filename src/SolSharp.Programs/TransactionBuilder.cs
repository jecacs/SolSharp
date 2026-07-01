using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// A fluent builder for legacy and v0 transactions: collect instructions, set the fee payer, recent
/// blockhash, and (for v0) address lookup tables, then compile and sign in one step.
/// </summary>
public sealed class TransactionBuilder
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<AddressLookupTableAccount> _lookupTables = [];
    private PublicKey? _feePayer;
    private string? _recentBlockhash;
    private Instruction? _nonceAdvance;

    /// <summary>Appends an instruction to the transaction.</summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instruction"/> is <c>null</c>.</exception>
    public TransactionBuilder AddInstruction(Instruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>Appends several instructions, in order.</summary>
    /// <param name="instructions">The instructions to add.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> is <c>null</c>.</exception>
    public TransactionBuilder AddInstructions(params Instruction[] instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        _instructions.AddRange(instructions);
        return this;
    }

    /// <summary>Sets the fee payer. If omitted, <see cref="Build"/> uses the first signer.</summary>
    /// <param name="feePayer">The account that pays the transaction fee.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public TransactionBuilder SetFeePayer(PublicKey feePayer)
    {
        _feePayer = feePayer;
        return this;
    }

    /// <summary>Sets the recent blockhash (base58) the transaction is anchored to.</summary>
    /// <param name="recentBlockhash">A recent blockhash, e.g. from <c>getLatestBlockhash</c>.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public TransactionBuilder SetRecentBlockhash(string recentBlockhash)
    {
        _recentBlockhash = recentBlockhash;
        return this;
    }

    /// <summary>
    /// Anchors the transaction to a durable nonce instead of a recent blockhash: <paramref name="nonce"/>
    /// takes the blockhash slot, and an <see cref="SystemProgram.AdvanceNonceAccount"/> instruction is
    /// prepended ahead of the added instructions, as the runtime requires. Replaces any previously set
    /// recent blockhash or durable nonce.
    /// </summary>
    /// <param name="nonceAccount">The durable nonce account.</param>
    /// <param name="authority">The nonce authority; must sign the transaction.</param>
    /// <param name="nonce">The account's current nonce value (base58), e.g. <c>NonceAccount.Nonce</c>.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nonce"/> is <c>null</c>.</exception>
    public TransactionBuilder SetDurableNonce(PublicKey nonceAccount, PublicKey authority, string nonce)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        _nonceAdvance = SystemProgram.AdvanceNonceAccount(nonceAccount, authority);
        _recentBlockhash = nonce;
        return this;
    }

    /// <summary>Sets the address lookup tables a v0 build (<see cref="BuildV0"/>) sources extra accounts from.</summary>
    /// <param name="lookupTables">The lookup tables; pass none to clear them.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lookupTables"/> is <c>null</c>.</exception>
    public TransactionBuilder SetAddressLookupTables(params AddressLookupTableAccount[] lookupTables)
    {
        ArgumentNullException.ThrowIfNull(lookupTables);
        _lookupTables.Clear();
        _lookupTables.AddRange(lookupTables);
        return this;
    }

    /// <summary>Compiles the collected instructions into an unsigned <see cref="Message"/>.</summary>
    /// <returns>The compiled message.</returns>
    /// <exception cref="InvalidOperationException">No fee payer, no recent blockhash, or no instructions were set.</exception>
    public Message BuildMessage()
    {
        var feePayer = _feePayer ?? throw new InvalidOperationException("A fee payer is required; call SetFeePayer.");
        return Compile(feePayer);
    }

    /// <summary>Compiles the message and signs it with <paramref name="signers"/>.</summary>
    /// <param name="signers">The signers to apply. When no fee payer was set, the first signer becomes the fee payer.</param>
    /// <returns>The signed transaction (unsigned if <paramref name="signers"/> is empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">No fee payer or signer, no recent blockhash, or no instructions were set.</exception>
    /// <exception cref="ArgumentException">A signer is not a required signer of the compiled message.</exception>
    public Transaction Build(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var feePayer = _feePayer ?? (signers.Length > 0
            ? signers[0].PublicKey
            : throw new InvalidOperationException("A fee payer is required; call SetFeePayer or pass a signer."));

        var transaction = Transaction.Create(Compile(feePayer));
        return signers.Length > 0 ? transaction.Sign(signers) : transaction;
    }

    private Message Compile(PublicKey feePayer)
    {
        if (_recentBlockhash is null)
            throw new InvalidOperationException("A recent blockhash is required; call SetRecentBlockhash.");
        if (_instructions.Count == 0)
            throw new InvalidOperationException("At least one instruction is required.");

        return Message.Compile(feePayer, _recentBlockhash, EffectiveInstructions());
    }

    // A durable-nonce transaction must run AdvanceNonceAccount as its first instruction.
    private IReadOnlyList<Instruction> EffectiveInstructions()
        => _nonceAdvance is null ? _instructions : [_nonceAdvance, .. _instructions];

    /// <summary>Compiles the collected instructions into an unsigned v0 <see cref="MessageV0"/>, using the set lookup tables.</summary>
    /// <returns>The compiled v0 message.</returns>
    /// <exception cref="InvalidOperationException">No fee payer, no recent blockhash, or no instructions were set.</exception>
    public MessageV0 BuildMessageV0()
    {
        var feePayer = _feePayer ?? throw new InvalidOperationException("A fee payer is required; call SetFeePayer.");
        return CompileV0(feePayer);
    }

    /// <summary>Compiles a v0 message (using the set lookup tables) and signs it with <paramref name="signers"/>.</summary>
    /// <param name="signers">The signers to apply. When no fee payer was set, the first signer becomes the fee payer.</param>
    /// <returns>The signed v0 transaction (unsigned if <paramref name="signers"/> is empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">No fee payer or signer, no recent blockhash, or no instructions were set.</exception>
    /// <exception cref="ArgumentException">A signer is not a required signer of the compiled message.</exception>
    public Transaction BuildV0(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);

        var feePayer = _feePayer ?? (signers.Length > 0
            ? signers[0].PublicKey
            : throw new InvalidOperationException("A fee payer is required; call SetFeePayer or pass a signer."));

        var transaction = Transaction.Create(CompileV0(feePayer));
        return signers.Length > 0 ? transaction.Sign(signers) : transaction;
    }

    private MessageV0 CompileV0(PublicKey feePayer)
    {
        if (_recentBlockhash is null)
            throw new InvalidOperationException("A recent blockhash is required; call SetRecentBlockhash.");
        if (_instructions.Count == 0)
            throw new InvalidOperationException("At least one instruction is required.");

        return MessageV0.Compile(feePayer, _recentBlockhash, EffectiveInstructions(), _lookupTables);
    }
}
