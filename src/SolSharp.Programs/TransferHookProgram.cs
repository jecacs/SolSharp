using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Fetches account data for generic off-chain SPL extra-account resolution.</summary>
/// <param name="address">The account to fetch.</param>
/// <param name="cancellationToken">A token that cancels the fetch.</param>
/// <returns>The account data, or <c>null</c> when the account does not exist.</returns>
public delegate ValueTask<ReadOnlyMemory<byte>?> ExtraAccountDataResolver(
    PublicKey address,
    CancellationToken cancellationToken);

/// <summary>Builders and off-chain account-resolution helpers for the SPL transfer-hook interface.</summary>
public static partial class TransferHookProgram
{
    private const int TlvHeaderLength = 8 + sizeof(uint);
    private const int ListHeaderLength = sizeof(uint);
    private const int MaxResolvableExtraAccountMetas = Message.MaxAccounts;
    private const byte ExternalProgramBit = 0x80;
    private static readonly byte[] ExecuteDiscriminator = [0x69, 0x25, 0x65, 0xc5, 0x4b, 0xfb, 0x66, 0x1a];
    private static readonly byte[] InitializeExtraAccountMetasDiscriminator = [0x2b, 0x22, 0x0d, 0x31, 0xa7, 0x58, 0xeb, 0xeb];
    private static readonly byte[] UpdateExtraAccountMetasDiscriminator = [0x9d, 0x69, 0x2a, 0x92, 0x66, 0x55, 0xf1, 0xae];
    private static readonly byte[] ExtraAccountMetasSeed = [.. "extra-account-metas"u8];

    /// <summary>Derives the validation-state PDA for a mint and transfer-hook program.</summary>
    /// <param name="mint">The Token-2022 mint.</param>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <returns>The canonical validation-state PDA.</returns>
    public static PublicKey GetExtraAccountMetasAddress(PublicKey mint, PublicKey hookProgramId)
        => ProgramDerivedAddress.FindProgramAddress([ExtraAccountMetasSeed, mint.ToBytes()], hookProgramId).Address;

    /// <summary>Gets the exact account-data size for one execute extra-account TLV entry.</summary>
    /// <param name="numberOfEntries">The number of 35-byte metadata entries.</param>
    /// <returns>The required number of bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfEntries"/> is negative or too large.</exception>
    public static int GetExtraAccountMetaListSize(int numberOfEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfEntries);
        try
        {
            return checked(TlvHeaderLength + ListHeaderLength + (numberOfEntries * ExtraAccountMeta.Length));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfEntries), numberOfEntries, "The metadata list is too large.");
        }
    }

    /// <summary>Builds the transfer-hook execute instruction without validation or additional accounts.</summary>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="source">The source token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="destination">The destination token account.</param>
    /// <param name="authority">The transfer authority.</param>
    /// <param name="amount">The transfer amount in base units.</param>
    /// <returns>The execute instruction.</returns>
    public static Instruction Execute(
        PublicKey hookProgramId,
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount)
    {
        var data = new byte[8 + sizeof(ulong)];
        ExecuteDiscriminator.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), amount);
        return new()
        {
            ProgramId = hookProgramId,
            Accounts =
            [
                AccountMeta.Readonly(source),
                AccountMeta.Readonly(mint),
                AccountMeta.Readonly(destination),
                AccountMeta.Readonly(authority)
            ],
            Data = data
        };
    }

    /// <summary>Builds execute with a validation account and already-resolved extra account metas.</summary>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="source">The source token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="destination">The destination token account.</param>
    /// <param name="authority">The transfer authority.</param>
    /// <param name="validationAccount">The validation-state account.</param>
    /// <param name="additionalAccounts">The resolved additional accounts, in validation-list order.</param>
    /// <param name="amount">The transfer amount in base units.</param>
    /// <returns>The execute instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="additionalAccounts"/> is <c>null</c>.</exception>
    public static Instruction ExecuteWithExtraAccountMetas(
        PublicKey hookProgramId,
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        PublicKey validationAccount,
        IReadOnlyList<AccountMeta> additionalAccounts,
        ulong amount)
    {
        ArgumentNullException.ThrowIfNull(additionalAccounts);
        var instruction = Execute(hookProgramId, source, mint, destination, authority, amount);
        var accounts = new List<AccountMeta>(5 + additionalAccounts.Count);
        accounts.AddRange(instruction.Accounts);
        accounts.Add(AccountMeta.Readonly(validationAccount));
        accounts.AddRange(additionalAccounts);
        return new() { ProgramId = hookProgramId, Accounts = accounts, Data = instruction.Data };
    }

    /// <summary>Builds the transfer-hook instruction that initializes a validation account's metadata list.</summary>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="validationAccount">The writable validation-state account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="mintAuthority">The mint authority; signs.</param>
    /// <param name="extraAccountMetas">The metadata entries to store.</param>
    /// <returns>The initialize-extra-account-metas instruction.</returns>
    public static Instruction InitializeExtraAccountMetaList(
        PublicKey hookProgramId,
        PublicKey validationAccount,
        PublicKey mint,
        PublicKey mintAuthority,
        IReadOnlyList<ExtraAccountMeta> extraAccountMetas)
        => new()
        {
            ProgramId = hookProgramId,
            Accounts =
            [
                AccountMeta.Writable(validationAccount),
                AccountMeta.Readonly(mint),
                AccountMeta.ReadonlySigner(mintAuthority),
                AccountMeta.Readonly(SystemProgram.ProgramId)
            ],
            Data = PackMetaListInstruction(InitializeExtraAccountMetasDiscriminator, extraAccountMetas)
        };

    /// <summary>Builds the transfer-hook instruction that replaces a validation account's metadata list.</summary>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="validationAccount">The writable validation-state account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="mintAuthority">The mint authority; signs.</param>
    /// <param name="extraAccountMetas">The replacement metadata entries.</param>
    /// <returns>The update-extra-account-metas instruction.</returns>
    public static Instruction UpdateExtraAccountMetaList(
        PublicKey hookProgramId,
        PublicKey validationAccount,
        PublicKey mint,
        PublicKey mintAuthority,
        IReadOnlyList<ExtraAccountMeta> extraAccountMetas)
        => new()
        {
            ProgramId = hookProgramId,
            Accounts =
            [
                AccountMeta.Writable(validationAccount),
                AccountMeta.Readonly(mint),
                AccountMeta.ReadonlySigner(mintAuthority)
            ],
            Data = PackMetaListInstruction(UpdateExtraAccountMetasDiscriminator, extraAccountMetas)
        };

    /// <summary>Encodes the execute metadata list as a complete generic SPL TLV account entry.</summary>
    /// <param name="extraAccountMetas">The metadata entries.</param>
    /// <returns>The validation-account bytes for one execute entry.</returns>
    public static byte[] EncodeExecuteExtraAccountMetaList(IReadOnlyList<ExtraAccountMeta> extraAccountMetas)
    {
        var value = PackMetaListValue(extraAccountMetas);
        var data = new byte[TlvHeaderLength + value.Length];
        ExecuteDiscriminator.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), checked((uint)value.Length));
        value.CopyTo(data, TlvHeaderLength);
        return data;
    }

    /// <summary>Decodes the first execute metadata list from generic SPL TLV validation-account data.</summary>
    /// <param name="validationAccountData">The complete validation-account data.</param>
    /// <returns>The decoded entries, or <c>null</c> when the TLV data is malformed or has no execute entry.</returns>
    public static IReadOnlyList<ExtraAccountMeta>? DecodeExecuteExtraAccountMetaList(
        ReadOnlySpan<byte> validationAccountData)
        => DecodeExecuteExtraAccountMetaList(validationAccountData, int.MaxValue, out _);

    /// <summary>Resolves the execute extra accounts from already-fetched validation-state data.</summary>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="source">The source token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="destination">The destination token account.</param>
    /// <param name="authority">The transfer authority.</param>
    /// <param name="amount">The transfer amount.</param>
    /// <param name="validationAccountData">The validation-state account data.</param>
    /// <param name="accountDataResolver">A client-agnostic account-data fetch function.</param>
    /// <param name="cancellationToken">A token that cancels account fetches.</param>
    /// <returns>The resolved extra account metas in validation-list order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accountDataResolver"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">
    /// The validation data or a metadata configuration is malformed, or the list exceeds the resolver's
    /// bounded budget of <see cref="Message.MaxAccounts"/> entries.
    /// </exception>
    /// <exception cref="InvalidOperationException">A required account or account-data slice cannot be resolved.</exception>
    public static async ValueTask<IReadOnlyList<AccountMeta>> ResolveExecuteExtraAccountMetasAsync(
        PublicKey hookProgramId,
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount,
        ReadOnlyMemory<byte> validationAccountData,
        ExtraAccountDataResolver accountDataResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountDataResolver);
        var configurations = DecodeExecuteExtraAccountMetaList(
            validationAccountData.Span,
            MaxResolvableExtraAccountMetas,
            out var exceedsEntryLimit);
        if (exceedsEntryLimit)
        {
            throw new FormatException(
                $"A transfer-hook execute metadata list may contain at most {MaxResolvableExtraAccountMetas} entries.");
        }

        if (configurations is null)
            throw new FormatException("The validation account does not contain a valid transfer-hook execute metadata list.");

        cancellationToken.ThrowIfCancellationRequested();
        var validationAccount = GetExtraAccountMetasAddress(mint, hookProgramId);
        var execute = Execute(hookProgramId, source, mint, destination, authority, amount);
        var workingAccounts = new List<AccountMeta>(5 + configurations.Length);
        workingAccounts.AddRange(execute.Accounts);
        workingAccounts.Add(AccountMeta.Readonly(validationAccount));
        var accountData = new List<ReadOnlyMemory<byte>?>(workingAccounts.Count + configurations.Length);
        var accountDataFetched = new List<bool>(workingAccounts.Count + configurations.Length);
        for (var i = 0; i < workingAccounts.Count; i++)
        {
            accountData.Add(null);
            accountDataFetched.Add(false);
        }

        accountData[execute.Accounts.Count] = validationAccountData;
        accountDataFetched[execute.Accounts.Count] = true;

        var resolved = new List<AccountMeta>(configurations.Length);
        for (var i = 0; i < configurations.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var accountIndex in RequiredAccountDataIndexes(configurations[i]))
            {
                var account = AccountAt(workingAccounts, accountIndex);
                if (!accountDataFetched[accountIndex])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    accountData[accountIndex] = await accountDataResolver(account.PublicKey, cancellationToken);
                    accountDataFetched[accountIndex] = true;
                }
            }

            var meta = Resolve(configurations[i], execute.Data, hookProgramId, workingAccounts, accountData);
            meta = DeEscalate(meta, workingAccounts);
            workingAccounts.Add(meta);
            accountData.Add(null);
            accountDataFetched.Add(false);
            resolved.Add(meta);
        }

        return resolved;
    }

    /// <summary>
    /// Fetches and resolves a mint's transfer-hook validation state, then appends the extra accounts plus the
    /// hook program and validation account to a Token-2022 transfer instruction.
    /// </summary>
    /// <param name="tokenInstruction">The Token-2022 transfer instruction to augment.</param>
    /// <param name="hookProgramId">The transfer-hook program.</param>
    /// <param name="source">The source token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="destination">The destination token account.</param>
    /// <param name="authority">The transfer authority.</param>
    /// <param name="amount">The transfer amount.</param>
    /// <param name="accountDataResolver">A client-agnostic account-data fetch function.</param>
    /// <param name="cancellationToken">A token that cancels account fetches.</param>
    /// <returns>A new instruction with the required accounts appended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokenInstruction"/> or <paramref name="accountDataResolver"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tokenInstruction"/> does not contain all four base execute accounts.</exception>
    /// <exception cref="InvalidOperationException">The validation account does not exist.</exception>
    public static async ValueTask<Instruction> AddExtraAccountsForExecuteAsync(
        Instruction tokenInstruction,
        PublicKey hookProgramId,
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount,
        ExtraAccountDataResolver accountDataResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenInstruction);
        ArgumentNullException.ThrowIfNull(accountDataResolver);
        foreach (var requiredKey in new[] { source, mint, destination, authority })
        {
            if (!tokenInstruction.Accounts.Any(account => account.PublicKey == requiredKey))
                throw new ArgumentException("The token instruction is missing a required transfer-hook base account.", nameof(tokenInstruction));
        }

        var validationAccount = GetExtraAccountMetasAddress(mint, hookProgramId);
        var validationData = await accountDataResolver(validationAccount, cancellationToken)
            ?? throw new InvalidOperationException($"The transfer-hook validation account {validationAccount} does not exist.");
        var extras = await ResolveExecuteExtraAccountMetasAsync(
            hookProgramId,
            source,
            mint,
            destination,
            authority,
            amount,
            validationData,
            accountDataResolver,
            cancellationToken);
        var accounts = new List<AccountMeta>(tokenInstruction.Accounts.Count + extras.Count + 2);
        accounts.AddRange(tokenInstruction.Accounts);
        accounts.AddRange(extras);
        accounts.Add(AccountMeta.Readonly(hookProgramId));
        accounts.Add(AccountMeta.Readonly(validationAccount));
        return new() { ProgramId = tokenInstruction.ProgramId, Accounts = accounts, Data = tokenInstruction.Data };
    }

    private static ExtraAccountMeta[]? DecodeExecuteExtraAccountMetaList(
        ReadOnlySpan<byte> validationAccountData,
        int maximumEntries,
        out bool exceedsEntryLimit)
    {
        exceedsEntryLimit = false;
        var offset = 0;
        while (offset < validationAccountData.Length)
        {
            var remaining = validationAccountData[offset..];
            if (remaining.Length < 8)
                return null;
            if (remaining[..8].IndexOfAnyExcept((byte)0) < 0)
                return null;
            if (remaining.Length < TlvHeaderLength)
                return null;

            var valueLength = BinaryPrimitives.ReadUInt32LittleEndian(remaining[8..]);
            if (valueLength > int.MaxValue || valueLength > remaining.Length - TlvHeaderLength)
                return null;
            var value = remaining.Slice(TlvHeaderLength, (int)valueLength);
            if (remaining[..8].SequenceEqual(ExecuteDiscriminator))
                return DecodeMetaListValue(value, maximumEntries, out exceedsEntryLimit);
            offset += TlvHeaderLength + (int)valueLength;
        }

        return null;
    }

    private static byte[] PackMetaListInstruction(
        ReadOnlySpan<byte> discriminator,
        IReadOnlyList<ExtraAccountMeta> extraAccountMetas)
    {
        var value = PackMetaListValue(extraAccountMetas);
        var data = new byte[8 + value.Length];
        discriminator.CopyTo(data);
        value.CopyTo(data, 8);
        return data;
    }

    private static byte[] PackMetaListValue(IReadOnlyList<ExtraAccountMeta> extraAccountMetas)
    {
        ArgumentNullException.ThrowIfNull(extraAccountMetas);
        int length;
        try
        {
            length = checked(ListHeaderLength + (extraAccountMetas.Count * ExtraAccountMeta.Length));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("The extra-account metadata list is too large.", nameof(extraAccountMetas), exception);
        }

        var data = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(data, checked((uint)extraAccountMetas.Count));
        for (var i = 0; i < extraAccountMetas.Count; i++)
        {
            var meta = extraAccountMetas[i]
                ?? throw new ArgumentNullException(nameof(extraAccountMetas), $"Metadata entry at index {i} is null.");
            meta.Encode().CopyTo(data, ListHeaderLength + (i * ExtraAccountMeta.Length));
        }

        return data;
    }

    private static ExtraAccountMeta[]? DecodeMetaListValue(
        ReadOnlySpan<byte> value,
        int maximumEntries,
        out bool exceedsEntryLimit)
    {
        exceedsEntryLimit = false;
        if (value.Length < ListHeaderLength)
            return null;
        var count = BinaryPrimitives.ReadUInt32LittleEndian(value);
        if (count > int.MaxValue)
            return null;
        if (count > (uint)maximumEntries)
        {
            exceedsEntryLimit = true;
            return null;
        }

        var requiredLength = ListHeaderLength + ((long)count * ExtraAccountMeta.Length);
        if (requiredLength > value.Length)
            return null;
        var entries = new ExtraAccountMeta[(int)count];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ExtraAccountMeta.Decode(value.Slice(ListHeaderLength + (i * ExtraAccountMeta.Length), ExtraAccountMeta.Length))!;
        }

        return entries;
    }

    private static AccountMeta Resolve(
        ExtraAccountMeta configuration,
        ReadOnlySpan<byte> instructionData,
        PublicKey executingProgram,
        IReadOnlyList<AccountMeta> accounts,
        IReadOnlyList<ReadOnlyMemory<byte>?> accountData)
    {
        PublicKey address;
        if (configuration.Discriminator == 0)
        {
            address = new(configuration.AddressConfigurationSpan);
        }
        else if (configuration.Discriminator is 1 or >= ExternalProgramBit)
        {
            var program = configuration.Discriminator == 1
                ? executingProgram
                : AccountAt(accounts, configuration.Discriminator - ExternalProgramBit).PublicKey;
            var seedConfigurations = configuration.DecodeSeeds()
                ?? throw new FormatException("An extra-account PDA has an invalid seed configuration.");
            var seeds = new byte[seedConfigurations.Count][];
            for (var i = 0; i < seedConfigurations.Count; i++)
                seeds[i] = ResolveSeed(seedConfigurations[i], instructionData, accounts, accountData);
            address = ProgramDerivedAddress.FindProgramAddress(seeds, program).Address;
        }
        else if (configuration.Discriminator == 2)
        {
            var data = configuration.AddressConfigurationSpan;
            address = data[0] switch
            {
                1 => ReadPublicKey(instructionData, data[1], "instruction data"),
                2 => ReadPublicKey(AccountDataAt(accountData, data[1]).Span, data[2], "account data"),
                _ => throw new FormatException("An extra-account public-key data configuration is invalid.")
            };
        }
        else
        {
            throw new FormatException($"Unsupported extra-account metadata discriminator {configuration.Discriminator}.");
        }

        return new(address, configuration.IsSigner, configuration.IsWritable);
    }

    private static List<byte> RequiredAccountDataIndexes(ExtraAccountMeta configuration)
    {
        if (configuration.Discriminator == 2)
        {
            var data = configuration.AddressConfigurationSpan;
            return data[0] == 2 ? [data[1]] : [];
        }

        if (configuration.Discriminator is not 1 and < ExternalProgramBit)
            return [];

        var seeds = configuration.DecodeSeeds()
            ?? throw new FormatException("An extra-account PDA has an invalid seed configuration.");
        var indexes = new List<byte>();
        foreach (var seed in seeds)
        {
            if (seed.Kind == ExtraAccountSeedKind.AccountData && !indexes.Contains(seed.AccountIndex))
                indexes.Add(seed.AccountIndex);
        }

        return indexes;
    }

    private static byte[] ResolveSeed(
        ExtraAccountSeed seed,
        ReadOnlySpan<byte> instructionData,
        IReadOnlyList<AccountMeta> accounts,
        IReadOnlyList<ReadOnlyMemory<byte>?> accountData)
        => seed.Kind switch
        {
            ExtraAccountSeedKind.Literal => seed.LiteralBytes.ToArray(),
            ExtraAccountSeedKind.InstructionData => ReadSlice(instructionData, seed.Index, seed.Length, "instruction data"),
            ExtraAccountSeedKind.AccountKey => AccountAt(accounts, seed.Index).PublicKey.ToBytes(),
            ExtraAccountSeedKind.AccountData => ReadSlice(
                AccountDataAt(accountData, seed.AccountIndex).Span,
                seed.DataIndex,
                seed.Length,
                "account data"),
            _ => throw new FormatException("Unknown extra-account seed kind.")
        };

    private static AccountMeta DeEscalate(AccountMeta requested, List<AccountMeta> existing)
    {
        var found = false;
        var writable = false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (existing[i].PublicKey != requested.PublicKey)
                continue;
            found = true;
            writable |= existing[i].IsWritable;
        }

        var resolvedWritable = requested.IsWritable && (!found || writable);
        return new(requested.PublicKey, isSigner: false, isWritable: resolvedWritable);
    }

    private static AccountMeta AccountAt(IReadOnlyList<AccountMeta> accounts, int index)
        => index < accounts.Count
            ? accounts[index]
            : throw new InvalidOperationException($"Extra-account resolution refers to missing account index {index}.");

    private static ReadOnlyMemory<byte> AccountDataAt(IReadOnlyList<ReadOnlyMemory<byte>?> accountData, int index)
    {
        if (index >= accountData.Count)
            throw new InvalidOperationException($"Extra-account resolution refers to missing account index {index}.");
        return accountData[index]
            ?? throw new InvalidOperationException($"Extra-account resolution requires data for account index {index}.");
    }

    private static PublicKey ReadPublicKey(ReadOnlySpan<byte> data, int index, string source)
        => new(ReadSlice(data, index, PublicKey.Length, source));

    private static byte[] ReadSlice(ReadOnlySpan<byte> data, int index, int length, string source)
    {
        if (index > data.Length || length > data.Length - index)
            throw new InvalidOperationException($"Extra-account resolution requests bytes outside the available {source}.");
        return [.. data.Slice(index, length)];
    }
}
