using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A compiled legacy Solana transaction message: the ordered account list, the header counts, the recent
/// blockhash, and the compiled instructions. Build one with <see cref="Compile"/>, then serialize it with
/// <see cref="Serialize"/> to get the bytes that are signed and sent.
/// </summary>
public sealed class Message : ITransactionMessage
{
    /// <summary>The maximum number of accounts a legacy message can reference (indices are single bytes).</summary>
    public const int MaxAccounts = 256;

    private Message(
        byte requiredSignatures,
        byte readonlySignedAccounts,
        byte readonlyUnsignedAccounts,
        IReadOnlyList<PublicKey> accountKeys,
        string recentBlockhash,
        IReadOnlyList<CompiledInstruction> instructions)
    {
        RequiredSignatures = requiredSignatures;
        ReadonlySignedAccounts = readonlySignedAccounts;
        ReadonlyUnsignedAccounts = readonlyUnsignedAccounts;
        AccountKeys = accountKeys;
        RecentBlockhash = recentBlockhash;
        Instructions = instructions;
    }

    /// <summary>Number of leading account keys that must sign the transaction.</summary>
    public byte RequiredSignatures { get; }

    /// <summary>How many of the signing accounts are read-only.</summary>
    public byte ReadonlySignedAccounts { get; }

    /// <summary>How many of the non-signing accounts are read-only.</summary>
    public byte ReadonlyUnsignedAccounts { get; }

    /// <summary>Every account the transaction references, ordered as the wire format requires (fee payer first).</summary>
    public IReadOnlyList<PublicKey> AccountKeys { get; }

    /// <summary>The recent blockhash (base58) the transaction is anchored to.</summary>
    public string RecentBlockhash { get; }

    /// <summary>The transaction's instructions, compiled to account-index form.</summary>
    public IReadOnlyList<CompiledInstruction> Instructions { get; }

    /// <summary>
    /// Compiles a set of instructions into a legacy message: deduplicates the accounts, merges their
    /// signer/writable flags, and orders them as Solana requires - the fee payer first, then every other
    /// account sorted by its bytes within the classes writable signers, read-only signers, writable
    /// non-signers, read-only non-signers - then indexes each instruction against that list.
    /// </summary>
    /// <param name="feePayer">The account that pays the fee; always the first account and a writable signer.</param>
    /// <param name="recentBlockhash">A recent blockhash (base58), e.g. from <c>getLatestBlockhash</c>.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <returns>The compiled message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recentBlockhash"/> or <paramref name="instructions"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The instructions reference more than <see cref="MaxAccounts"/> distinct accounts.</exception>
    public static Message Compile(PublicKey feePayer, string recentBlockhash, IReadOnlyList<Instruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(recentBlockhash);
        ArgumentNullException.ThrowIfNull(instructions);

        var flags = new Dictionary<PublicKey, AccountFlags>();

        void Merge(PublicKey key, bool signer, bool writable)
        {
            flags.TryGetValue(key, out var current);
            flags[key] = new AccountFlags(current.IsSigner || signer, current.IsWritable || writable);
        }

        Merge(feePayer, signer: true, writable: true);
        foreach (var instruction in instructions)
        {
            foreach (var account in instruction.Accounts)
                Merge(account.PublicKey, account.IsSigner, account.IsWritable);

            Merge(instruction.ProgramId, signer: false, writable: false);
        }

        if (flags.Count > MaxAccounts)
            throw new ArgumentException($"A legacy message can reference at most {MaxAccounts} accounts, got {flags.Count}.", nameof(instructions));

        var rest = new List<PublicKey>(flags.Count);
        foreach (var key in flags.Keys)
            if (key != feePayer)
                rest.Add(key);

        rest.Sort(CompareByBytes);

        var orderedKeys = new List<PublicKey>(flags.Count) { feePayer };
        AddClass(orderedKeys, rest, flags, signer: true, writable: true);
        AddClass(orderedKeys, rest, flags, signer: true, writable: false);
        AddClass(orderedKeys, rest, flags, signer: false, writable: true);
        AddClass(orderedKeys, rest, flags, signer: false, writable: false);

        byte requiredSignatures = 0, readonlySigned = 0, readonlyUnsigned = 0;
        var finalPosition = new Dictionary<PublicKey, int>(orderedKeys.Count);
        for (var slot = 0; slot < orderedKeys.Count; slot++)
        {
            var key = orderedKeys[slot];
            finalPosition[key] = slot;

            var meta = flags[key];
            if (meta.IsSigner)
            {
                requiredSignatures++;
                if (!meta.IsWritable)
                    readonlySigned++;
            }
            else if (!meta.IsWritable)
            {
                readonlyUnsigned++;
            }
        }

        var compiled = new CompiledInstruction[instructions.Count];
        for (var n = 0; n < instructions.Count; n++)
        {
            var instruction = instructions[n];
            var accountIndexes = new byte[instruction.Accounts.Count];
            for (var a = 0; a < instruction.Accounts.Count; a++)
                accountIndexes[a] = (byte)finalPosition[instruction.Accounts[a].PublicKey];

            compiled[n] = new CompiledInstruction
            {
                ProgramIdIndex = (byte)finalPosition[instruction.ProgramId],
                AccountIndexes = accountIndexes,
                Data = instruction.Data
            };
        }

        return new Message(requiredSignatures, readonlySigned, readonlyUnsigned, orderedKeys, recentBlockhash, compiled);
    }

    /// <summary>Serializes the message to its canonical wire bytes - the bytes a signer signs over.</summary>
    /// <returns>The serialized message.</returns>
    /// <exception cref="FormatException"><see cref="RecentBlockhash"/> is not a 32-byte base58 value.</exception>
    public byte[] Serialize()
    {
        if (!Base58.TryDecode(RecentBlockhash, out var blockhash) || blockhash.Length != PublicKey.Length)
            throw new FormatException($"Recent blockhash must be a 32-byte base58 value, got '{RecentBlockhash}'.");

        using var buffer = new MemoryStream(256);
        buffer.WriteByte(RequiredSignatures);
        buffer.WriteByte(ReadonlySignedAccounts);
        buffer.WriteByte(ReadonlyUnsignedAccounts);

        buffer.Write(ShortVec.Encode(AccountKeys.Count));
        foreach (var key in AccountKeys)
            buffer.Write(key.ToBytes());

        buffer.Write(blockhash);

        buffer.Write(ShortVec.Encode(Instructions.Count));
        foreach (var instruction in Instructions)
        {
            buffer.WriteByte(instruction.ProgramIdIndex);
            buffer.Write(ShortVec.Encode(instruction.AccountIndexes.Length));
            buffer.Write(instruction.AccountIndexes);
            buffer.Write(ShortVec.Encode(instruction.Data.Length));
            buffer.Write(instruction.Data);
        }

        return buffer.ToArray();
    }

    /// <summary>Resolves the compiled instructions back into <see cref="Instruction"/>s, with each account's key and signer/writable flags.</summary>
    /// <param name="lookupTables">Ignored for a legacy message, which loads no lookup-table accounts.</param>
    /// <returns>The resolved instructions, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lookupTables"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An account index is out of range.</exception>
    public IReadOnlyList<Instruction> DecompileInstructions(IReadOnlyList<AddressLookupTableAccount> lookupTables)
    {
        ArgumentNullException.ThrowIfNull(lookupTables);
        return MessageDecompiler.Decompile(
            Instructions, AccountKeys, RequiredSignatures, ReadonlySignedAccounts, ReadonlyUnsignedAccounts, AccountKeys.Count, numLoadedWritable: 0);
    }

    /// <summary>Parses a legacy message from its wire bytes.</summary>
    /// <param name="data">The serialized message (no version prefix).</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="FormatException">The data is truncated, or a compact-u16 length in it is malformed.</exception>
    public static Message Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            var offset = 0;
            var requiredSignatures = data[offset++];
            var readonlySignedAccounts = data[offset++];
            var readonlyUnsignedAccounts = data[offset++];

            var accountKeys = MessageWire.ReadAccountKeys(data, ref offset);

            var recentBlockhash = new PublicKey(data.Slice(offset, PublicKey.Length)).ToString();
            offset += PublicKey.Length;

            var instructions = MessageWire.ReadInstructions(data, ref offset);

            return new Message(requiredSignatures, readonlySignedAccounts, readonlyUnsignedAccounts, accountKeys, recentBlockhash, instructions);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Span indexing and slicing throw index errors on short input; surface the documented type.
            throw new FormatException("The message data is truncated.", exception);
        }
    }

    private static void AddClass(
        List<PublicKey> target,
        List<PublicKey> sortedRest,
        Dictionary<PublicKey, AccountFlags> flags,
        bool signer,
        bool writable)
    {
        foreach (var key in sortedRest)
        {
            var meta = flags[key];
            if (meta.IsSigner == signer && meta.IsWritable == writable)
                target.Add(key);
        }
    }

    private static int CompareByBytes(PublicKey a, PublicKey b)
    {
        Span<byte> x = stackalloc byte[PublicKey.Length];
        Span<byte> y = stackalloc byte[PublicKey.Length];
        a.CopyTo(x);
        b.CopyTo(y);
        return x.SequenceCompareTo(y);
    }

    private readonly record struct AccountFlags(bool IsSigner, bool IsWritable);
}
