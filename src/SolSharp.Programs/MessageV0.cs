using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A compiled v0 (versioned) transaction message: like a legacy <see cref="Message"/>, but able to load
/// extra accounts from on-chain address lookup tables so a transaction can reference far more accounts.
/// Build one with <see cref="Compile"/>, then <see cref="Serialize"/> for the signed-and-sent bytes, which
/// begin with the <see cref="VersionPrefix"/> byte.
/// </summary>
public sealed class MessageV0 : ITransactionMessage
{
    /// <summary>The leading byte marking a v0 message: the high bit set over version number 0.</summary>
    public const byte VersionPrefix = 0x80;

    /// <summary>The maximum number of distinct accounts a message can reference (indices are single bytes).</summary>
    public const int MaxAccounts = 256;

    private MessageV0(
        byte requiredSignatures,
        byte readonlySignedAccounts,
        byte readonlyUnsignedAccounts,
        IReadOnlyList<PublicKey> accountKeys,
        string recentBlockhash,
        IReadOnlyList<CompiledInstruction> instructions,
        IReadOnlyList<MessageAddressTableLookup> addressTableLookups)
    {
        RequiredSignatures = requiredSignatures;
        ReadonlySignedAccounts = readonlySignedAccounts;
        ReadonlyUnsignedAccounts = readonlyUnsignedAccounts;
        AccountKeys = accountKeys;
        RecentBlockhash = recentBlockhash;
        Instructions = instructions;
        AddressTableLookups = addressTableLookups;
    }

    /// <inheritdoc/>
    public byte RequiredSignatures { get; }

    /// <summary>How many of the signing accounts are read-only.</summary>
    public byte ReadonlySignedAccounts { get; }

    /// <summary>How many of the non-signing static accounts are read-only.</summary>
    public byte ReadonlyUnsignedAccounts { get; }

    /// <summary>The static account keys, fee payer first and signers leading; lookup-loaded accounts are not listed here.</summary>
    public IReadOnlyList<PublicKey> AccountKeys { get; }

    /// <summary>The recent blockhash (base58) the transaction is anchored to.</summary>
    public string RecentBlockhash { get; }

    /// <summary>The transaction's instructions, compiled against the static keys followed by the lookup-loaded accounts.</summary>
    public IReadOnlyList<CompiledInstruction> Instructions { get; }

    /// <summary>The address-lookup-table references this message loads its extra accounts from.</summary>
    public IReadOnlyList<MessageAddressTableLookup> AddressTableLookups { get; }

    /// <summary>
    /// Compiles instructions into a v0 message: moves every eligible account (a non-signer that is not
    /// itself a program) found in a supplied lookup table out of the static keys and into a table lookup,
    /// then orders the remaining static keys as Solana requires - fee payer first, then the rest sorted by
    /// their bytes within the classes writable signers, read-only signers, writable non-signers, read-only
    /// non-signers - and indexes each instruction against the static keys followed by the loaded accounts.
    /// </summary>
    /// <param name="feePayer">The account that pays the fee; always the first static account and a writable signer.</param>
    /// <param name="recentBlockhash">A recent blockhash (base58), e.g. from <c>getLatestBlockhash</c>.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <param name="addressLookupTables">The lookup tables to source extra accounts from; pass an empty list for none.</param>
    /// <returns>The compiled v0 message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recentBlockhash"/>, <paramref name="instructions"/>, or <paramref name="addressLookupTables"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The instructions reference more than <see cref="MaxAccounts"/> distinct accounts.</exception>
    public static MessageV0 Compile(
        PublicKey feePayer,
        string recentBlockhash,
        IReadOnlyList<Instruction> instructions,
        IReadOnlyList<AddressLookupTableAccount> addressLookupTables)
    {
        ArgumentNullException.ThrowIfNull(recentBlockhash);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(addressLookupTables);

        // Merge each account's signer/writable flags, and mark program ids as invoked - invoked keys and
        // signers must stay in the static keys and can never be sourced from a lookup table.
        var metas = new Dictionary<PublicKey, KeyMeta>();

        void Merge(PublicKey key, bool signer, bool writable, bool invoked)
        {
            metas.TryGetValue(key, out var current);
            metas[key] = new KeyMeta(current.IsSigner || signer, current.IsWritable || writable, current.IsInvoked || invoked);
        }

        Merge(feePayer, signer: true, writable: true, invoked: false);
        foreach (var instruction in instructions)
        {
            Merge(instruction.ProgramId, signer: false, writable: false, invoked: true);
            foreach (var account in instruction.Accounts)
                Merge(account.PublicKey, account.IsSigner, account.IsWritable, invoked: false);
        }

        if (metas.Count > MaxAccounts)
            throw new ArgumentException($"A message can reference at most {MaxAccounts} accounts, got {metas.Count}.", nameof(instructions));

        // Solana keys its compilation off a BTreeMap, so every "walk the accounts" step works in public-key
        // byte order: draining into tables and partitioning the static keys both rely on this ordering.
        var sortedKeys = new List<PublicKey>(metas.Keys);
        sortedKeys.Sort(CompareByBytes);

        var lookups = new List<MessageAddressTableLookup>();
        var loadedWritable = new List<PublicKey>();
        var loadedReadonly = new List<PublicKey>();
        var drained = new HashSet<PublicKey>();

        foreach (var table in addressLookupTables)
        {
            var writableIndexes = new List<byte>();
            var readonlyIndexes = new List<byte>();
            var tableWritable = new List<PublicKey>();
            var tableReadonly = new List<PublicKey>();

            foreach (var key in sortedKeys)
            {
                if (drained.Contains(key))
                    continue;

                var meta = metas[key];
                if (meta.IsSigner || meta.IsInvoked || !meta.IsWritable)
                    continue;

                var index = IndexInTable(table.Addresses, key);
                if (index < 0)
                    continue;

                writableIndexes.Add((byte)index);
                tableWritable.Add(key);
                drained.Add(key);
            }

            foreach (var key in sortedKeys)
            {
                if (drained.Contains(key))
                    continue;

                var meta = metas[key];
                if (meta.IsSigner || meta.IsInvoked || meta.IsWritable)
                    continue;

                var index = IndexInTable(table.Addresses, key);
                if (index < 0)
                    continue;

                readonlyIndexes.Add((byte)index);
                tableReadonly.Add(key);
                drained.Add(key);
            }

            // A table that contributed no accounts is left out entirely.
            if (writableIndexes.Count == 0 && readonlyIndexes.Count == 0)
                continue;

            lookups.Add(new MessageAddressTableLookup
            {
                AccountKey = table.Key,
                WritableIndexes = [.. writableIndexes],
                ReadonlyIndexes = [.. readonlyIndexes]
            });
            loadedWritable.AddRange(tableWritable);
            loadedReadonly.AddRange(tableReadonly);
        }

        var staticRemaining = new List<PublicKey>(sortedKeys.Count);
        foreach (var key in sortedKeys)
            if (!drained.Contains(key) && key != feePayer)
                staticRemaining.Add(key);

        var orderedStatic = new List<PublicKey>(staticRemaining.Count + 1) { feePayer };
        AddClass(orderedStatic, staticRemaining, metas, signer: true, writable: true);
        AddClass(orderedStatic, staticRemaining, metas, signer: true, writable: false);
        AddClass(orderedStatic, staticRemaining, metas, signer: false, writable: true);
        AddClass(orderedStatic, staticRemaining, metas, signer: false, writable: false);

        byte requiredSignatures = 0, readonlySigned = 0, readonlyUnsigned = 0;
        foreach (var key in orderedStatic)
        {
            var meta = metas[key];
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

        // Instruction indices address the static keys first, then the loaded writables, then the loaded read-onlys.
        var position = new Dictionary<PublicKey, int>(metas.Count);
        var slot = 0;
        foreach (var key in orderedStatic)
            position[key] = slot++;
        foreach (var key in loadedWritable)
            position[key] = slot++;
        foreach (var key in loadedReadonly)
            position[key] = slot++;

        var compiled = new CompiledInstruction[instructions.Count];
        for (var n = 0; n < instructions.Count; n++)
        {
            var instruction = instructions[n];
            var accountIndexes = new byte[instruction.Accounts.Count];
            for (var a = 0; a < instruction.Accounts.Count; a++)
                accountIndexes[a] = (byte)position[instruction.Accounts[a].PublicKey];

            compiled[n] = new CompiledInstruction
            {
                ProgramIdIndex = (byte)position[instruction.ProgramId],
                AccountIndexes = accountIndexes,
                Data = instruction.Data
            };
        }

        return new MessageV0(requiredSignatures, readonlySigned, readonlyUnsigned, orderedStatic, recentBlockhash, compiled, lookups);
    }

    /// <summary>Serializes the message to its canonical wire bytes - what a signer signs over - starting with <see cref="VersionPrefix"/>.</summary>
    /// <returns>The serialized v0 message.</returns>
    /// <exception cref="FormatException"><see cref="RecentBlockhash"/> is not a 32-byte base58 value.</exception>
    public byte[] Serialize()
    {
        if (!Base58.TryDecode(RecentBlockhash, out var blockhash) || blockhash.Length != PublicKey.Length)
            throw new FormatException($"Recent blockhash must be a 32-byte base58 value, got '{RecentBlockhash}'.");

        using var buffer = new MemoryStream(256);
        buffer.WriteByte(VersionPrefix);
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

        buffer.Write(ShortVec.Encode(AddressTableLookups.Count));
        foreach (var lookup in AddressTableLookups)
        {
            buffer.Write(lookup.AccountKey.ToBytes());
            buffer.Write(ShortVec.Encode(lookup.WritableIndexes.Length));
            buffer.Write(lookup.WritableIndexes);
            buffer.Write(ShortVec.Encode(lookup.ReadonlyIndexes.Length));
            buffer.Write(lookup.ReadonlyIndexes);
        }

        return buffer.ToArray();
    }

    /// <summary>Parses a v0 message from its wire bytes (including the leading <see cref="VersionPrefix"/> byte).</summary>
    /// <param name="data">The serialized v0 message.</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="FormatException">The data is not a versioned message, or a compact-u16 length is malformed.</exception>
    public static MessageV0 Deserialize(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var prefix = data[offset++];
        if ((prefix & VersionPrefix) == 0)
            throw new FormatException("Not a versioned message: the high bit of the version prefix is not set.");

        var requiredSignatures = data[offset++];
        var readonlySignedAccounts = data[offset++];
        var readonlyUnsignedAccounts = data[offset++];

        var accountKeys = MessageWire.ReadAccountKeys(data, ref offset);

        var recentBlockhash = new PublicKey(data.Slice(offset, PublicKey.Length)).ToString();
        offset += PublicKey.Length;

        var instructions = MessageWire.ReadInstructions(data, ref offset);

        var lookupCount = ShortVec.Decode(data[offset..], out var read);
        offset += read;
        var addressTableLookups = new MessageAddressTableLookup[lookupCount];
        for (var i = 0; i < lookupCount; i++)
        {
            var accountKey = new PublicKey(data.Slice(offset, PublicKey.Length));
            offset += PublicKey.Length;

            var writableCount = ShortVec.Decode(data[offset..], out read);
            offset += read;
            var writableIndexes = data.Slice(offset, writableCount).ToArray();
            offset += writableCount;

            var readonlyCount = ShortVec.Decode(data[offset..], out read);
            offset += read;
            var readonlyIndexes = data.Slice(offset, readonlyCount).ToArray();
            offset += readonlyCount;

            addressTableLookups[i] = new MessageAddressTableLookup
            {
                AccountKey = accountKey,
                WritableIndexes = writableIndexes,
                ReadonlyIndexes = readonlyIndexes
            };
        }

        return new MessageV0(requiredSignatures, readonlySignedAccounts, readonlyUnsignedAccounts, accountKeys, recentBlockhash, instructions, addressTableLookups);
    }

    private static void AddClass(
        List<PublicKey> target,
        List<PublicKey> sortedRemaining,
        Dictionary<PublicKey, KeyMeta> metas,
        bool signer,
        bool writable)
    {
        foreach (var key in sortedRemaining)
        {
            var meta = metas[key];
            if (meta.IsSigner == signer && meta.IsWritable == writable)
                target.Add(key);
        }
    }

    private static int IndexInTable(IReadOnlyList<PublicKey> addresses, PublicKey key)
    {
        for (var i = 0; i < addresses.Count; i++)
            if (addresses[i] == key)
                return i;

        return -1;
    }

    // Mirrors Message.CompareByBytes; kept local so the v0 compiler does not reach into the legacy one.
    private static int CompareByBytes(PublicKey a, PublicKey b)
    {
        Span<byte> x = stackalloc byte[PublicKey.Length];
        Span<byte> y = stackalloc byte[PublicKey.Length];
        a.CopyTo(x);
        b.CopyTo(y);
        return x.SequenceCompareTo(y);
    }

    private readonly record struct KeyMeta(bool IsSigner, bool IsWritable, bool IsInvoked);
}
