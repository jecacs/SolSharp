using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A compiled legacy Solana transaction message: the ordered account list, the header counts, the recent
/// blockhash, and the compiled instructions. Build one with <c>Compile</c>, then serialize it with
/// <see cref="Serialize()"/> to get the bytes that are signed and sent.
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
    /// <param name="recentBlockhash">A recent blockhash, e.g. from <c>getLatestBlockhash</c>.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <returns>The compiled message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The instructions reference more than <see cref="MaxAccounts"/> distinct accounts or require more
    /// than 127 signatures, whose high bit would collide with the versioned-message prefix.
    /// </exception>
    public static Message Compile(PublicKey feePayer, Hash recentBlockhash, IReadOnlyList<Instruction> instructions)
        => Compile(feePayer, recentBlockhash.ToString(), instructions);

    /// <summary>
    /// Compiles a set of instructions into a legacy message using a base58 blockhash string.
    /// </summary>
    /// <param name="feePayer">The account that pays the fee; always the first account and a writable signer.</param>
    /// <param name="recentBlockhash">A recent blockhash (base58), e.g. from <c>getLatestBlockhash</c>.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <returns>The compiled message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recentBlockhash"/> or <paramref name="instructions"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The instructions reference more than <see cref="MaxAccounts"/> distinct accounts or require more
    /// than 127 signatures, whose high bit would collide with the versioned-message prefix.
    /// </exception>
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

        var requiredSignatures = 0;
        var readonlySigned = 0;
        var readonlyUnsigned = 0;
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

        // A legacy message has no separate version byte: the high bit of its first header byte is
        // the versioned-message discriminator. Keep the signer count below it so the serialized
        // message cannot be mistaken for v0 (or a future version).
        if (requiredSignatures >= MessageV0.VersionPrefix)
            throw new ArgumentException(
                $"A legacy message can require at most {MessageV0.VersionPrefix - 1} signatures, got {requiredSignatures}.",
                nameof(instructions));

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
                Data = [.. instruction.Data]
            };
        }

        return new Message(
            (byte)requiredSignatures,
            (byte)readonlySigned,
            (byte)readonlyUnsigned,
            orderedKeys,
            recentBlockhash,
            compiled);
    }

    /// <summary>Parses a legacy message from its wire bytes.</summary>
    /// <param name="data">The serialized message (no version prefix).</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="FormatException">
    /// The data is truncated, contains trailing bytes, has a malformed compact-u16 length, or breaks a rule
    /// Solana's sanitize enforces: header counts that overlap the account list or leave no writable
    /// fee-payer signer, an instruction whose program id is the fee payer, or an out-of-range program id
    /// or account index.
    /// </exception>
    public static Message Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            var offset = 0;
            var requiredSignatures = data[offset++];
            if ((requiredSignatures & MessageV0.VersionPrefix) != 0)
                throw new FormatException(
                    $"A legacy message signer count must be below {MessageV0.VersionPrefix}; " +
                    $"the high bit marks a versioned message, got {requiredSignatures}.");

            var readonlySignedAccounts = data[offset++];
            var readonlyUnsignedAccounts = data[offset++];

            var accountKeys = MessageWire.ReadAccountKeys(data, ref offset);

            var recentBlockhash = new PublicKey(data.Slice(offset, PublicKey.Length)).ToString();
            offset += PublicKey.Length;

            var instructions = MessageWire.ReadInstructions(data, ref offset);

            if (offset != data.Length)
                throw new FormatException($"The message has {data.Length - offset} trailing byte(s).");

            // Mirror Solana's sanitize so a message the network would refuse never parses successfully.
            MessageWire.SanitizeHeader(requiredSignatures, readonlySignedAccounts, readonlyUnsignedAccounts, accountKeys.Length);
            MessageWire.SanitizeInstructions(instructions, accountKeys.Length, accountKeys.Length);

            return new Message(requiredSignatures, readonlySignedAccounts, readonlyUnsignedAccounts, accountKeys, recentBlockhash, instructions);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Span indexing and slicing throw index errors on short input; surface the documented type.
            throw new FormatException("The message data is truncated.", exception);
        }
    }

    /// <summary>Serializes the message to its canonical wire bytes - the bytes a signer signs over.</summary>
    /// <returns>The serialized message.</returns>
    /// <exception cref="FormatException"><see cref="RecentBlockhash"/> is not a 32-byte base58 value.</exception>
    public byte[] Serialize()
    {
        var buffer = new byte[GetSerializedLength()];
        Serialize(buffer);
        return buffer;
    }

    /// <summary>Returns the exact length of the serialized message, in bytes.</summary>
    /// <returns>The serialized length.</returns>
    public int GetSerializedLength()
    {
        var length = 3 // the header counts
            + ShortVec.GetByteCount(AccountKeys.Count) + (AccountKeys.Count * PublicKey.Length)
            + PublicKey.Length // the recent blockhash
            + ShortVec.GetByteCount(Instructions.Count);

        foreach (var instruction in Instructions)
            length += 1
                + ShortVec.GetByteCount(instruction.AccountIndexes.Length) + instruction.AccountIndexes.Length
                + ShortVec.GetByteCount(instruction.Data.Length) + instruction.Data.Length;

        return length;
    }

    /// <summary>Serializes the message into <paramref name="destination"/> without allocating.</summary>
    /// <param name="destination">The span to write into; must be at least <see cref="GetSerializedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is smaller than <see cref="GetSerializedLength"/> bytes.</exception>
    /// <exception cref="FormatException"><see cref="RecentBlockhash"/> is not a 32-byte base58 value.</exception>
    public int Serialize(Span<byte> destination)
    {
        if (!Base58.TryDecode(RecentBlockhash, out var blockhash) || blockhash.Length != PublicKey.Length)
            throw new FormatException($"Recent blockhash must be a 32-byte base58 value, got '{RecentBlockhash}'.");

        var required = GetSerializedLength();
        if (destination.Length < required)
            throw new ArgumentException($"Destination must be at least {required} bytes.", nameof(destination));

        var offset = 0;
        destination[offset++] = RequiredSignatures;
        destination[offset++] = ReadonlySignedAccounts;
        destination[offset++] = ReadonlyUnsignedAccounts;

        offset += ShortVec.Encode(AccountKeys.Count, destination[offset..]);
        foreach (var key in AccountKeys)
        {
            key.CopyTo(destination[offset..]);
            offset += PublicKey.Length;
        }

        blockhash.CopyTo(destination[offset..]);
        offset += PublicKey.Length;

        offset += ShortVec.Encode(Instructions.Count, destination[offset..]);
        foreach (var instruction in Instructions)
        {
            destination[offset++] = instruction.ProgramIdIndex;
            offset += ShortVec.Encode(instruction.AccountIndexes.Length, destination[offset..]);
            instruction.AccountIndexes.CopyTo(destination[offset..]);
            offset += instruction.AccountIndexes.Length;
            offset += ShortVec.Encode(instruction.Data.Length, destination[offset..]);
            instruction.Data.CopyTo(destination[offset..]);
            offset += instruction.Data.Length;
        }

        return offset;
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
