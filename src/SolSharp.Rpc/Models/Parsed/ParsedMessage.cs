using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>The message of a <c>jsonParsed</c> transaction: its accounts, instructions, and recent blockhash.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedMessage
{
    /// <summary>The accounts the transaction references, in index order, each with its role flags.</summary>
    [JsonPropertyName("accountKeys")]
    [JsonRequired]
    public IReadOnlyList<ParsedAccountKey> AccountKeys
    {
        get => field!;
        init => field = RequireNonNullEntries(value, "account keys");
    }

    /// <summary>The top-level instructions, in execution order.</summary>
    [JsonPropertyName("instructions")]
    [JsonRequired]
    public IReadOnlyList<ParsedInstruction> Instructions
    {
        get => field!;
        init => field = RequireNonNullEntries(value, "instructions");
    }

    /// <summary>The recent blockhash the transaction was built against (base58).</summary>
    [JsonPropertyName("recentBlockhash")]
    [JsonRequired]
    public string RecentBlockhash
    {
        get => field!;
        init => field = value ?? throw new JsonException("A parsed message must carry a recent blockhash.");
    }

    /// <summary>The address lookup-table references of a versioned message; absent for legacy messages.</summary>
    [JsonPropertyName("addressTableLookups")]
    public IReadOnlyList<ParsedAddressTableLookup>? AddressTableLookups
    {
        get;
        init => field = value is null
            ? null
            : RequireNonNullEntries(value, "address-table lookups");
    }

    /// <summary>
    /// The message-level execution configuration for a version-1 transaction; absent for legacy and v0
    /// messages.
    /// </summary>
    [JsonPropertyName("transactionConfig")]
    public ParsedTransactionConfig? TransactionConfig { get; init; }

    private static IReadOnlyList<T> RequireNonNullEntries<T>(IReadOnlyList<T>? values, string name)
        where T : class
    {
        if (values is null || values.Any(static value => value is null))
            throw new JsonException($"A parsed message must carry only non-null {name}.");

        return values;
    }
}
