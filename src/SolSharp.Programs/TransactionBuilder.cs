using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs;

/// <summary>
/// A fluent builder for legacy, v0, and SIMD-0385 V1 transactions: collect instructions, set the fee
/// payer and lifetime specifier, optionally set lookup tables for v0 or inline configuration for V1,
/// then compile and sign in one step.
/// </summary>
public sealed class TransactionBuilder
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<AddressLookupTableAccount> _lookupTables = [];
    private PublicKey? _feePayer;
    private string? _recentBlockhash;
    private Instruction? _nonceAdvance;
    private TransactionConfigV1 _v1Config = new();

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
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> or one of its elements is <c>null</c>.</exception>
    public TransactionBuilder AddInstructions(params Instruction[] instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        foreach (var instruction in instructions)
            ArgumentNullException.ThrowIfNull(instruction, nameof(instructions));

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

    /// <summary>
    /// Sets the recent blockhash (base58) the transaction is anchored to. Replaces any previously set
    /// durable nonce (<c>SetDurableNonce</c>), dropping its prepended advance-nonce instruction -
    /// the two anchoring modes are mutually exclusive.
    /// </summary>
    /// <param name="recentBlockhash">A recent blockhash, e.g. from <c>getLatestBlockhash</c>.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recentBlockhash"/> is <c>null</c>.</exception>
    public TransactionBuilder SetRecentBlockhash(string recentBlockhash)
    {
        ArgumentNullException.ThrowIfNull(recentBlockhash);
        _recentBlockhash = recentBlockhash;
        _nonceAdvance = null;
        return this;
    }

    /// <summary>
    /// Sets the typed recent blockhash the transaction is anchored to. Replaces any previously set
    /// durable nonce and drops its prepended advance-nonce instruction.
    /// </summary>
    /// <param name="recentBlockhash">A recent blockhash, e.g. from <c>getLatestBlockhash</c>.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public TransactionBuilder SetRecentBlockhash(Hash recentBlockhash)
        => SetRecentBlockhash(recentBlockhash.ToString());

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

    /// <summary>
    /// Anchors the transaction to a typed durable nonce and prepends the required advance-nonce instruction.
    /// Replaces any previously set recent blockhash or durable nonce.
    /// </summary>
    /// <param name="nonceAccount">The durable nonce account.</param>
    /// <param name="authority">The nonce authority; must sign the transaction.</param>
    /// <param name="nonce">The account's current nonce value.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public TransactionBuilder SetDurableNonce(PublicKey nonceAccount, PublicKey authority, Hash nonce)
        => SetDurableNonce(nonceAccount, authority, nonce.ToString());

    /// <summary>Sets the address lookup tables a v0 build (<see cref="BuildV0"/>) sources extra accounts from.</summary>
    /// <param name="lookupTables">The lookup tables; pass none to clear them.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lookupTables"/> or one of its elements is <c>null</c>.</exception>
    public TransactionBuilder SetAddressLookupTables(params AddressLookupTableAccount[] lookupTables)
    {
        ArgumentNullException.ThrowIfNull(lookupTables);
        foreach (var lookupTable in lookupTables)
            ArgumentNullException.ThrowIfNull(lookupTable, nameof(lookupTables));

        _lookupTables.Clear();
        _lookupTables.AddRange(lookupTables);
        return this;
    }

    /// <summary>
    /// Sets the inline compute, loaded-account-data, heap, and total priority-fee configuration used by
    /// <see cref="BuildMessageV1"/> and <see cref="BuildV1"/>. Unspecified compute-unit and loaded-data
    /// limits have the SIMD-0385 value zero; an unspecified heap uses <see cref="MessageV1.DefaultHeapSize"/>.
    /// </summary>
    /// <param name="config">The V1 transaction configuration.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <c>null</c>.</exception>
    public TransactionBuilder SetV1Config(TransactionConfigV1 config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _v1Config = config;
        return this;
    }

    /// <summary>Compiles the collected instructions into an unsigned <see cref="Message"/>.</summary>
    /// <returns>The compiled message.</returns>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or recent blockhash was set, or neither a user instruction nor a durable-nonce advance is present.
    /// </exception>
    public Message BuildMessage()
    {
        var feePayer = _feePayer ?? throw new InvalidOperationException("A fee payer is required; call SetFeePayer.");
        return Compile(feePayer);
    }

    /// <summary>Compiles the message and signs it with <paramref name="signers"/>.</summary>
    /// <param name="signers">The signers to apply. When no fee payer was set, the first signer becomes the fee payer.</param>
    /// <returns>The signed transaction (unsigned if <paramref name="signers"/> is empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or signer or recent blockhash was set, or neither a user instruction nor a durable-nonce advance is present.
    /// </exception>
    /// <exception cref="ArgumentException">A signer is not a required signer of the compiled message.</exception>
    public Transaction Build(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);
        ValidateSigners(signers);

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
        if (_instructions.Count == 0 && _nonceAdvance is null)
            throw new InvalidOperationException("At least one instruction is required.");

        return Message.Compile(feePayer, _recentBlockhash, EffectiveInstructions());
    }

    // A durable-nonce transaction must run AdvanceNonceAccount as its first instruction.
    private List<Instruction> EffectiveInstructions()
        => _nonceAdvance is null ? _instructions : [_nonceAdvance, .. _instructions];

    /// <summary>Compiles the collected instructions into an unsigned v0 <see cref="MessageV0"/>, using the set lookup tables.</summary>
    /// <returns>The compiled v0 message.</returns>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or recent blockhash was set, or neither a user instruction nor a durable-nonce advance is present.
    /// </exception>
    public MessageV0 BuildMessageV0()
    {
        var feePayer = _feePayer ?? throw new InvalidOperationException("A fee payer is required; call SetFeePayer.");
        return CompileV0(feePayer);
    }

    /// <summary>Compiles a v0 message (using the set lookup tables) and signs it with <paramref name="signers"/>.</summary>
    /// <param name="signers">The signers to apply. When no fee payer was set, the first signer becomes the fee payer.</param>
    /// <returns>The signed v0 transaction (unsigned if <paramref name="signers"/> is empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or signer or recent blockhash was set, or neither a user instruction nor a durable-nonce advance is present.
    /// </exception>
    /// <exception cref="ArgumentException">A signer is not a required signer of the compiled message.</exception>
    public Transaction BuildV0(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);
        ValidateSigners(signers);

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
        if (_instructions.Count == 0 && _nonceAdvance is null)
            throw new InvalidOperationException("At least one instruction is required.");

        return MessageV0.Compile(feePayer, _recentBlockhash, EffectiveInstructions(), _lookupTables);
    }

    /// <summary>Compiles the collected instructions and inline configuration into an unsigned V1 message.</summary>
    /// <returns>The compiled SIMD-0385 V1 message.</returns>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or lifetime specifier was set, no instruction is present, or address lookup tables were
    /// supplied even though V1 stores all addresses inline.
    /// </exception>
    /// <exception cref="ArgumentException">The message or configuration exceeds a V1 wire limit.</exception>
    /// <remarks>
    /// If <see cref="SetV1Config"/> was not called, the message uses an empty configuration whose
    /// compute-unit and loaded-account-data limits are zero and is normally unsuitable for submission.
    /// </remarks>
    public MessageV1 BuildMessageV1()
    {
        var feePayer = _feePayer ?? throw new InvalidOperationException("A fee payer is required; call SetFeePayer.");
        return CompileV1(feePayer);
    }

    /// <summary>Compiles a V1 message and signs it with <paramref name="signers"/>.</summary>
    /// <param name="signers">The signers to apply. When no fee payer was set, the first signer becomes the fee payer.</param>
    /// <returns>The signed V1 transaction (unsigned if <paramref name="signers"/> is empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signers"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No fee payer or signer or lifetime specifier was set, no instruction is present, or address lookup
    /// tables were supplied even though V1 stores all addresses inline.
    /// </exception>
    /// <exception cref="ArgumentException">A signer is not required, or the message/configuration exceeds a V1 limit.</exception>
    /// <remarks>
    /// If <see cref="SetV1Config"/> was not called, the transaction uses an empty configuration whose
    /// compute-unit and loaded-account-data limits are zero and is normally unsuitable for submission.
    /// </remarks>
    public Transaction BuildV1(params ISigner[] signers)
    {
        ArgumentNullException.ThrowIfNull(signers);
        ValidateSigners(signers);

        var feePayer = _feePayer ?? (signers.Length > 0
            ? signers[0].PublicKey
            : throw new InvalidOperationException("A fee payer is required; call SetFeePayer or pass a signer."));

        var transaction = Transaction.Create(CompileV1(feePayer));
        return signers.Length > 0 ? transaction.Sign(signers) : transaction;
    }

    private MessageV1 CompileV1(PublicKey feePayer)
    {
        if (_recentBlockhash is null)
            throw new InvalidOperationException("A lifetime specifier is required; call SetRecentBlockhash or SetDurableNonce.");
        if (_instructions.Count == 0 && _nonceAdvance is null)
            throw new InvalidOperationException("At least one instruction is required.");
        if (_lookupTables.Count != 0)
            throw new InvalidOperationException("V1 messages do not support address lookup tables; clear them before building V1.");

        return MessageV1.Compile(feePayer, _recentBlockhash, EffectiveInstructions(), _v1Config);
    }

    private static void ValidateSigners(IReadOnlyList<ISigner> signers)
    {
        foreach (var signer in signers)
            ArgumentNullException.ThrowIfNull(signer, nameof(signers));
    }
}
