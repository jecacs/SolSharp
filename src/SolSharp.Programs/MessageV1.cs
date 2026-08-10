using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A SIMD-0385 V1 transaction message. V1 stores every account inline, carries compute configuration
/// in its header, and uses fixed-width instruction headers instead of compact-u16 lengths.
/// </summary>
public sealed class MessageV1 : ITransactionMessage
{
    /// <summary>The leading byte that identifies a V1 message.</summary>
    public const byte VersionPrefix = 0x81;

    /// <summary>
    /// The maximum V1 transaction wire size accepted by current RPC and runtime paths. The wire codec
    /// intentionally does not enforce this admission limit and can round-trip larger payloads, matching
    /// the pinned Solana SDK.
    /// </summary>
    public const int MaxTransactionSize = 4096;

    /// <summary>The maximum number of inline account addresses in a V1 message.</summary>
    public const int MaxAccounts = 64;

    /// <summary>The maximum number of instructions in a V1 message.</summary>
    public const int MaxInstructions = 64;

    /// <summary>The maximum number of required signatures in a V1 transaction.</summary>
    public const int MaxSignatures = 12;

    /// <summary>The default V1 transaction heap size when no explicit value is present (32 KiB).</summary>
    public const uint DefaultHeapSize = MinHeapSize;

    /// <summary>The minimum explicit V1 transaction heap size (32 KiB).</summary>
    public const uint MinHeapSize = 32 * 1024;

    /// <summary>The maximum explicit V1 transaction heap size (256 KiB).</summary>
    public const uint MaxHeapSize = 256 * 1024;

    private const uint PriorityFeeMask = 0b11;
    private const uint ComputeUnitLimitMask = 0b100;
    private const uint LoadedAccountsDataSizeMask = 0b1000;
    private const uint HeapSizeMask = 0b10000;
    private const uint KnownConfigMask = 0b11111;
    private const int FixedBodyLength = 3 + sizeof(uint) + Hash.Length + 1 + 1;

    private MessageV1(
        byte requiredSignatures,
        byte readonlySignedAccounts,
        byte readonlyUnsignedAccounts,
        TransactionConfigV1 config,
        Hash lifetimeSpecifier,
        IReadOnlyList<PublicKey> accountKeys,
        IReadOnlyList<CompiledInstruction> instructions)
    {
        RequiredSignatures = requiredSignatures;
        ReadonlySignedAccounts = readonlySignedAccounts;
        ReadonlyUnsignedAccounts = readonlyUnsignedAccounts;
        Config = config;
        LifetimeSpecifier = lifetimeSpecifier;
        AccountKeys = accountKeys;
        Instructions = instructions;
    }

    /// <inheritdoc/>
    public byte RequiredSignatures { get; }

    /// <summary>How many of the signing accounts are read-only.</summary>
    public byte ReadonlySignedAccounts { get; }

    /// <summary>How many of the non-signing accounts are read-only.</summary>
    public byte ReadonlyUnsignedAccounts { get; }

    /// <summary>The inline V1 compute and priority-fee configuration.</summary>
    public TransactionConfigV1 Config { get; }

    /// <summary>The recent blockhash or durable nonce that limits the transaction lifetime.</summary>
    public Hash LifetimeSpecifier { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PublicKey> AccountKeys { get; }

    /// <inheritdoc/>
    public IReadOnlyList<CompiledInstruction> Instructions { get; }

    /// <summary>Compiles instructions into a V1 message with an empty inline configuration.</summary>
    /// <param name="feePayer">The writable signer that pays the transaction fee.</param>
    /// <param name="lifetimeSpecifier">The recent blockhash or durable nonce.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <returns>The compiled V1 message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> or an element is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The instructions cannot be represented by the V1 limits.</exception>
    /// <remarks>
    /// The empty configuration encodes compute-unit and loaded-account-data limits of zero and normally
    /// should be replaced with explicit limits before submitting the transaction.
    /// </remarks>
    public static MessageV1 Compile(
        PublicKey feePayer,
        Hash lifetimeSpecifier,
        IReadOnlyList<Instruction> instructions)
        => Compile(feePayer, lifetimeSpecifier, instructions, new TransactionConfigV1());

    /// <summary>Compiles instructions into a V1 message with an empty inline configuration.</summary>
    /// <param name="feePayer">The writable signer that pays the transaction fee.</param>
    /// <param name="lifetimeSpecifier">The base58 recent blockhash or durable nonce.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <returns>The compiled V1 message.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="lifetimeSpecifier"/>, <paramref name="instructions"/>, or an instruction is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="lifetimeSpecifier"/> is not a 32-byte base58 hash, or the instructions cannot be
    /// represented by the V1 limits.
    /// </exception>
    /// <remarks>
    /// The empty configuration encodes compute-unit and loaded-account-data limits of zero and normally
    /// should be replaced with explicit limits before submitting the transaction.
    /// </remarks>
    public static MessageV1 Compile(
        PublicKey feePayer,
        string lifetimeSpecifier,
        IReadOnlyList<Instruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(lifetimeSpecifier);
        return Compile(feePayer, new Hash(lifetimeSpecifier), instructions, new TransactionConfigV1());
    }

    /// <summary>Compiles instructions and inline configuration into a V1 message.</summary>
    /// <param name="feePayer">The writable signer that pays the transaction fee.</param>
    /// <param name="lifetimeSpecifier">The recent blockhash or durable nonce.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <param name="config">The inline compute and priority-fee configuration.</param>
    /// <returns>The compiled V1 message.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="instructions"/>, an instruction, or <paramref name="config"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The configuration is invalid or the instructions cannot be represented by the V1 limits.
    /// </exception>
    public static MessageV1 Compile(
        PublicKey feePayer,
        Hash lifetimeSpecifier,
        IReadOnlyList<Instruction> instructions,
        TransactionConfigV1 config)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfigForCompile(config);

        if (instructions.Count > MaxInstructions)
            throw new ArgumentException(
                $"A V1 message can contain at most {MaxInstructions} instructions, got {instructions.Count}.",
                nameof(instructions));

        var flags = new Dictionary<PublicKey, AccountFlags>();

        void Merge(PublicKey key, bool signer, bool writable)
        {
            flags.TryGetValue(key, out var current);
            flags[key] = new AccountFlags(current.IsSigner || signer, current.IsWritable || writable);
        }

        Merge(feePayer, signer: true, writable: true);
        foreach (var instruction in instructions)
        {
            ArgumentNullException.ThrowIfNull(instruction, nameof(instructions));
            ArgumentNullException.ThrowIfNull(instruction.Accounts, nameof(instructions));
            ArgumentNullException.ThrowIfNull(instruction.Data, nameof(instructions));

            if (instruction.ProgramId == feePayer)
                throw new ArgumentException("The V1 fee payer cannot also be an invoked program.", nameof(instructions));
            if (instruction.Accounts.Count > byte.MaxValue)
                throw new ArgumentException(
                    $"A V1 instruction can reference at most {byte.MaxValue} account slots, got {instruction.Accounts.Count}.",
                    nameof(instructions));
            if (instruction.Data.Length > ushort.MaxValue)
                throw new ArgumentException(
                    $"A V1 instruction can carry at most {ushort.MaxValue} data bytes, got {instruction.Data.Length}.",
                    nameof(instructions));

            foreach (var account in instruction.Accounts)
                Merge(account.PublicKey, account.IsSigner, account.IsWritable);

            Merge(instruction.ProgramId, signer: false, writable: false);
        }

        if (flags.Count > MaxAccounts)
            throw new ArgumentException(
                $"A V1 message can reference at most {MaxAccounts} accounts, got {flags.Count}.",
                nameof(instructions));

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
        var positions = new Dictionary<PublicKey, int>(orderedKeys.Count);
        for (var index = 0; index < orderedKeys.Count; index++)
        {
            var key = orderedKeys[index];
            positions[key] = index;
            var accountFlags = flags[key];
            if (accountFlags.IsSigner)
            {
                requiredSignatures++;
                if (!accountFlags.IsWritable)
                    readonlySigned++;
            }
            else if (!accountFlags.IsWritable)
            {
                readonlyUnsigned++;
            }
        }

        if (requiredSignatures > MaxSignatures)
            throw new ArgumentException(
                $"A V1 message can require at most {MaxSignatures} signatures, got {requiredSignatures}.",
                nameof(instructions));

        var compiled = new CompiledInstruction[instructions.Count];
        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            var accountIndexes = new byte[instruction.Accounts.Count];
            for (var accountIndex = 0; accountIndex < instruction.Accounts.Count; accountIndex++)
                accountIndexes[accountIndex] = (byte)positions[instruction.Accounts[accountIndex].PublicKey];

            compiled[index] = new CompiledInstruction
            {
                ProgramIdIndex = (byte)positions[instruction.ProgramId],
                AccountIndexes = accountIndexes,
                Data = [.. instruction.Data]
            };
        }

        return new MessageV1(
            (byte)requiredSignatures,
            (byte)readonlySigned,
            (byte)readonlyUnsigned,
            config,
            lifetimeSpecifier,
            orderedKeys,
            compiled);
    }

    /// <summary>Compiles instructions and inline configuration into a V1 message.</summary>
    /// <param name="feePayer">The writable signer that pays the transaction fee.</param>
    /// <param name="lifetimeSpecifier">The base58 recent blockhash or durable nonce.</param>
    /// <param name="instructions">The instructions to include, in execution order.</param>
    /// <param name="config">The inline compute and priority-fee configuration.</param>
    /// <returns>The compiled V1 message.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="lifetimeSpecifier"/>, <paramref name="instructions"/>, an instruction, or
    /// <paramref name="config"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="lifetimeSpecifier"/> is not a 32-byte base58 hash, the configuration is invalid,
    /// or the instructions cannot be represented by the V1 limits.
    /// </exception>
    public static MessageV1 Compile(
        PublicKey feePayer,
        string lifetimeSpecifier,
        IReadOnlyList<Instruction> instructions,
        TransactionConfigV1 config)
    {
        ArgumentNullException.ThrowIfNull(lifetimeSpecifier);
        return Compile(feePayer, new Hash(lifetimeSpecifier), instructions, config);
    }

    /// <summary>Parses one complete version-prefixed V1 message and applies all sanitize checks.</summary>
    /// <param name="data">The complete V1 message bytes, beginning with <see cref="VersionPrefix"/>.</param>
    /// <returns>The parsed V1 message.</returns>
    /// <exception cref="FormatException">
    /// The message is truncated, has trailing data, uses an invalid config mask, or violates a V1
    /// sanitize constraint.
    /// </exception>
    public static MessageV1 Deserialize(ReadOnlySpan<byte> data)
        => DeserializeCore(data, requireExactLength: true, out _);

    /// <summary>Validates all SIMD-0385 header, configuration, account, and instruction constraints.</summary>
    /// <exception cref="FormatException">The message violates a V1 sanitize constraint.</exception>
    public void Validate()
    {
        if (RequiredSignatures > MaxSignatures)
            throw new FormatException($"A V1 message may require at most {MaxSignatures} signatures.");
        if (Instructions.Count > MaxInstructions)
            throw new FormatException($"A V1 message may contain at most {MaxInstructions} instructions.");
        if (AccountKeys.Count > MaxAccounts)
            throw new FormatException($"A V1 message may contain at most {MaxAccounts} account addresses.");
        if (AccountKeys.Count < RequiredSignatures + ReadonlyUnsignedAccounts)
            throw new FormatException(
                "The V1 account list cannot satisfy the required-signature and read-only unsigned header counts.");
        if (ReadonlySignedAccounts >= RequiredSignatures)
            throw new FormatException("A V1 message must have at least one writable signer for the fee payer.");

        var uniqueKeys = new HashSet<PublicKey>();
        foreach (var key in AccountKeys)
            if (!uniqueKeys.Add(key))
                throw new FormatException("A V1 message cannot contain duplicate account addresses.");

        ValidateConfigForWire(Config);

        foreach (var instruction in Instructions)
        {
            if (instruction is null)
                throw new FormatException("A V1 compiled instruction cannot be null.");
            if (instruction.AccountIndexes is null || instruction.Data is null)
                throw new FormatException("A V1 compiled instruction must contain account-index and data arrays.");
            if (instruction.ProgramIdIndex == 0 || instruction.ProgramIdIndex >= AccountKeys.Count)
                throw new FormatException("A V1 instruction program id must reference a non-payer inline account.");
            if (instruction.AccountIndexes.Length > byte.MaxValue)
                throw new FormatException($"A V1 instruction may reference at most {byte.MaxValue} account slots.");
            if (instruction.Data.Length > ushort.MaxValue)
                throw new FormatException($"A V1 instruction may contain at most {ushort.MaxValue} data bytes.");

            foreach (var accountIndex in instruction.AccountIndexes)
                if (accountIndex >= AccountKeys.Count)
                    throw new FormatException(
                        $"V1 instruction account index {accountIndex} is outside {AccountKeys.Count} account addresses.");
        }
    }

    /// <inheritdoc/>
    public byte[] Serialize()
    {
        var bytes = new byte[GetSerializedLength()];
        Serialize(bytes);
        return bytes;
    }

    /// <inheritdoc/>
    public int GetSerializedLength()
    {
        var length = 1 + FixedBodyLength
            + (AccountKeys.Count * PublicKey.Length)
            + GetConfigSerializedLength(Config)
            + (Instructions.Count * 4);

        foreach (var instruction in Instructions)
            length += instruction.AccountIndexes.Length + instruction.Data.Length;

        return length;
    }

    /// <inheritdoc/>
    public int Serialize(Span<byte> destination)
    {
        Validate();
        var length = GetSerializedLength();
        if (destination.Length < length)
            throw new ArgumentException($"Destination must be at least {length} bytes.", nameof(destination));

        var offset = 0;
        destination[offset++] = VersionPrefix;
        destination[offset++] = RequiredSignatures;
        destination[offset++] = ReadonlySignedAccounts;
        destination[offset++] = ReadonlyUnsignedAccounts;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], GetConfigMask(Config));
        offset += sizeof(uint);
        LifetimeSpecifier.CopyTo(destination[offset..]);
        offset += Hash.Length;
        destination[offset++] = (byte)Instructions.Count;
        destination[offset++] = (byte)AccountKeys.Count;

        foreach (var key in AccountKeys)
        {
            key.CopyTo(destination[offset..]);
            offset += PublicKey.Length;
        }

        offset += WriteConfig(Config, destination[offset..]);

        foreach (var instruction in Instructions)
        {
            destination[offset++] = instruction.ProgramIdIndex;
            destination[offset++] = (byte)instruction.AccountIndexes.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], (ushort)instruction.Data.Length);
            offset += sizeof(ushort);
        }

        foreach (var instruction in Instructions)
        {
            instruction.AccountIndexes.CopyTo(destination[offset..]);
            offset += instruction.AccountIndexes.Length;
            instruction.Data.CopyTo(destination[offset..]);
            offset += instruction.Data.Length;
        }

        return offset;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Instruction> DecompileInstructions(IReadOnlyList<AddressLookupTableAccount> lookupTables)
    {
        ArgumentNullException.ThrowIfNull(lookupTables);
        return DecompileInstructions();
    }

    /// <summary>
    /// Resolves the compiled instructions back into client instructions with their inline account keys and
    /// signer/writable flags. V1 has no address lookup tables to resolve.
    /// </summary>
    /// <returns>The resolved instructions, in execution order.</returns>
    /// <exception cref="ArgumentException">An instruction contains an account index outside the inline key list.</exception>
    public IReadOnlyList<Instruction> DecompileInstructions()
        => MessageDecompiler.Decompile(
            Instructions,
            AccountKeys,
            RequiredSignatures,
            ReadonlySignedAccounts,
            ReadonlyUnsignedAccounts,
            AccountKeys.Count,
            numLoadedWritable: 0);

    internal static MessageV1 DeserializeTransactionMessage(ReadOnlySpan<byte> data, out int consumed)
        => DeserializeCore(data, requireExactLength: false, out consumed);

    private static MessageV1 DeserializeCore(ReadOnlySpan<byte> data, bool requireExactLength, out int consumed)
    {
        try
        {
            if (data.Length == 0 || data[0] != VersionPrefix)
                throw new FormatException($"A V1 message must begin with version byte 0x{VersionPrefix:X2}.");
            if (data.Length < 1 + FixedBodyLength)
                throw new FormatException("The V1 message data is truncated in its fixed header.");

            var offset = 1;
            var requiredSignatures = data[offset++];
            var readonlySignedAccounts = data[offset++];
            var readonlyUnsignedAccounts = data[offset++];
            var configMask = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
            offset += sizeof(uint);

            if ((configMask & ~KnownConfigMask) != 0 || HasPartialPriorityFeeMask(configMask))
                throw new FormatException($"Invalid V1 transaction config mask 0x{configMask:X8}.");

            var lifetimeSpecifier = new Hash(data.Slice(offset, Hash.Length));
            offset += Hash.Length;
            var instructionCount = data[offset++];
            var accountCount = data[offset++];

            if (requiredSignatures > MaxSignatures)
                throw new FormatException($"A V1 message may require at most {MaxSignatures} signatures.");
            if (instructionCount > MaxInstructions)
                throw new FormatException($"A V1 message may contain at most {MaxInstructions} instructions.");
            if (accountCount > MaxAccounts)
                throw new FormatException($"A V1 message may contain at most {MaxAccounts} account addresses.");

            EnsureRemaining(data, offset, accountCount * PublicKey.Length, "account addresses");
            var accountKeys = new List<PublicKey>(accountCount);
            for (var index = 0; index < accountCount; index++)
            {
                accountKeys.Add(new PublicKey(data.Slice(offset, PublicKey.Length)));
                offset += PublicKey.Length;
            }

            ulong? priorityFee = null;
            uint? computeUnitLimit = null;
            uint? loadedAccountsDataSizeLimit = null;
            uint? heapSize = null;
            if ((configMask & PriorityFeeMask) == PriorityFeeMask)
            {
                EnsureRemaining(data, offset, sizeof(ulong), "priority fee");
                priorityFee = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
                offset += sizeof(ulong);
            }

            if ((configMask & ComputeUnitLimitMask) != 0)
            {
                EnsureRemaining(data, offset, sizeof(uint), "compute-unit limit");
                computeUnitLimit = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
                offset += sizeof(uint);
            }

            if ((configMask & LoadedAccountsDataSizeMask) != 0)
            {
                EnsureRemaining(data, offset, sizeof(uint), "loaded-account-data limit");
                loadedAccountsDataSizeLimit = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
                offset += sizeof(uint);
            }

            if ((configMask & HeapSizeMask) != 0)
            {
                EnsureRemaining(data, offset, sizeof(uint), "heap size");
                heapSize = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
                offset += sizeof(uint);
            }

            EnsureRemaining(data, offset, instructionCount * 4, "instruction headers");
            var headers = new InstructionHeader[instructionCount];
            long payloadLength = 0;
            for (var index = 0; index < instructionCount; index++)
            {
                var programIdIndex = data[offset++];
                var instructionAccountCount = data[offset++];
                var instructionDataLength = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                offset += sizeof(ushort);
                headers[index] = new InstructionHeader(
                    programIdIndex,
                    instructionAccountCount,
                    instructionDataLength);
                payloadLength += instructionAccountCount + instructionDataLength;
            }

            if (payloadLength > data.Length - offset)
                throw new FormatException(
                    $"The V1 instruction payloads need {payloadLength} byte(s), but only {data.Length - offset} remain.");

            var instructions = new CompiledInstruction[instructionCount];
            for (var index = 0; index < instructionCount; index++)
            {
                var header = headers[index];
                var accountIndexes = data.Slice(offset, header.AccountCount).ToArray();
                offset += header.AccountCount;
                var instructionData = data.Slice(offset, header.DataLength).ToArray();
                offset += header.DataLength;
                instructions[index] = new CompiledInstruction
                {
                    ProgramIdIndex = header.ProgramIdIndex,
                    AccountIndexes = accountIndexes,
                    Data = instructionData
                };
            }

            if (requireExactLength && offset != data.Length)
                throw new FormatException($"The V1 message contains {data.Length - offset} trailing byte(s).");

            var config = new TransactionConfigV1
            {
                PriorityFee = priorityFee,
                ComputeUnitLimit = computeUnitLimit,
                LoadedAccountsDataSizeLimit = loadedAccountsDataSizeLimit,
                HeapSize = heapSize
            };
            var message = new MessageV1(
                requiredSignatures,
                readonlySignedAccounts,
                readonlyUnsignedAccounts,
                config,
                lifetimeSpecifier,
                accountKeys,
                instructions);
            message.Validate();
            consumed = offset;
            return message;
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            throw new FormatException("The V1 message data is truncated.", exception);
        }
    }

    private static uint GetConfigMask(TransactionConfigV1 config)
    {
        var mask = 0u;
        if (config.PriorityFee.HasValue)
            mask |= PriorityFeeMask;
        if (config.ComputeUnitLimit.HasValue)
            mask |= ComputeUnitLimitMask;
        if (config.LoadedAccountsDataSizeLimit.HasValue)
            mask |= LoadedAccountsDataSizeMask;
        if (config.HeapSize.HasValue)
            mask |= HeapSizeMask;
        return mask;
    }

    private static int GetConfigSerializedLength(TransactionConfigV1 config)
    {
        var length = 0;
        if (config.PriorityFee.HasValue)
            length += sizeof(ulong);
        if (config.ComputeUnitLimit.HasValue)
            length += sizeof(uint);
        if (config.LoadedAccountsDataSizeLimit.HasValue)
            length += sizeof(uint);
        if (config.HeapSize.HasValue)
            length += sizeof(uint);
        return length;
    }

    private static int WriteConfig(TransactionConfigV1 config, Span<byte> destination)
    {
        var offset = 0;
        if (config.PriorityFee is { } priorityFee)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], priorityFee);
            offset += sizeof(ulong);
        }

        if (config.ComputeUnitLimit is { } computeUnitLimit)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], computeUnitLimit);
            offset += sizeof(uint);
        }

        if (config.LoadedAccountsDataSizeLimit is { } loadedAccountsDataSizeLimit)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], loadedAccountsDataSizeLimit);
            offset += sizeof(uint);
        }

        if (config.HeapSize is { } heapSize)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], heapSize);
            offset += sizeof(uint);
        }

        return offset;
    }

    private static bool HasPartialPriorityFeeMask(uint mask)
    {
        var priorityBits = mask & PriorityFeeMask;
        return priorityBits is not 0 and not PriorityFeeMask;
    }

    private static void ValidateConfigForCompile(TransactionConfigV1 config)
    {
        if (config.HeapSize is { } heapSize
            && (heapSize < MinHeapSize || heapSize > MaxHeapSize || heapSize % 1024 != 0))
        {
            throw new ArgumentException(
                $"A V1 heap size must be a 1024-byte multiple from {MinHeapSize} through {MaxHeapSize}.",
                nameof(config));
        }
    }

    private static void ValidateConfigForWire(TransactionConfigV1 config)
    {
        if (config.HeapSize is { } heapSize
            && (heapSize < MinHeapSize || heapSize > MaxHeapSize || heapSize % 1024 != 0))
        {
            throw new FormatException(
                $"A V1 heap size must be a 1024-byte multiple from {MinHeapSize} through {MaxHeapSize}.");
        }
    }

    private static void EnsureRemaining(ReadOnlySpan<byte> data, int offset, int needed, string field)
    {
        if (needed > data.Length - offset)
            throw new FormatException(
                $"The V1 message is truncated in {field}: need {needed} byte(s), but only {data.Length - offset} remain.");
    }

    private static void AddClass(
        List<PublicKey> target,
        IReadOnlyList<PublicKey> sortedRest,
        IReadOnlyDictionary<PublicKey, AccountFlags> flags,
        bool signer,
        bool writable)
    {
        foreach (var key in sortedRest)
        {
            var accountFlags = flags[key];
            if (accountFlags.IsSigner == signer && accountFlags.IsWritable == writable)
                target.Add(key);
        }
    }

    private static int CompareByBytes(PublicKey left, PublicKey right)
    {
        Span<byte> leftBytes = stackalloc byte[PublicKey.Length];
        Span<byte> rightBytes = stackalloc byte[PublicKey.Length];
        left.CopyTo(leftBytes);
        right.CopyTo(rightBytes);
        return leftBytes.SequenceCompareTo(rightBytes);
    }

    private readonly record struct AccountFlags(bool IsSigner, bool IsWritable);

    private readonly record struct InstructionHeader(byte ProgramIdIndex, byte AccountCount, ushort DataLength);
}
