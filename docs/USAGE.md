# SolSharp — Usage Guide

A task-oriented tour of SolSharp with copy-pasteable C# examples. For the high-level overview and design
notes see the [README](../README.md); for conventions and architecture see [CLAUDE.md](../CLAUDE.md).

Every snippet targets **.NET 8** and uses the single `SolSharp` NuGet package, which bundles all four
assemblies — the namespaces `SolSharp.Core.*`, `SolSharp.Rpc`, `SolSharp.Wallet`, and `SolSharp.Programs`.

## Contents

- [Installation](#installation)
- [Creating a client](#creating-a-client)
- [Keys and wallets](#keys-and-wallets)
- [SOL and lamports](#sol-and-lamports)
- [Reading accounts](#reading-accounts)
- [SPL token accounts and mints](#spl-token-accounts-and-mints)
- [Sending your first transaction](#sending-your-first-transaction)
- [Simulating before sending](#simulating-before-sending)
- [Priority fees (compute budget)](#priority-fees-compute-budget)
- [SPL token transfers](#spl-token-transfers)
- [Attaching a memo](#attaching-a-memo)
- [Versioned (v0) transactions and address lookup tables](#versioned-v0-transactions-and-address-lookup-tables)
- [Decoding a transaction](#decoding-a-transaction)
- [Reading parsed transactions](#reading-parsed-transactions)
- [Cluster and validator info](#cluster-and-validator-info)
- [WebSocket subscriptions](#websocket-subscriptions)
- [Confirming a transaction](#confirming-a-transaction)
- [Durable nonces](#durable-nonces)
- [Program-derived addresses (PDAs)](#program-derived-addresses-pdas)
- [Rate limits, custom endpoints, and headers](#rate-limits-custom-endpoints-and-headers)
- [Error handling](#error-handling)

## Installation

SolSharp ships as one NuGet package that bundles all four assemblies:

```bash
dotnet add package SolSharp
```

That single reference brings in every namespace — `SolSharp.Core.*`, `SolSharp.Rpc`, `SolSharp.Wallet`, and
`SolSharp.Programs` — so there's no juggling which project to add. Requires .NET 8 or later.

## Creating a client

The HTTP client is a typed `HttpClient` registered through dependency injection, so it gets a resilience
pipeline (retry on transient errors and HTTP 429) for free.

```csharp
using Microsoft.Extensions.DependencyInjection;
using SolSharp.Rpc;

// In an app with a DI container (ASP.NET, Worker, Generic Host):
services.AddSolanaRpc("https://api.mainnet-beta.solana.com");
// ...then inject SolanaRpcClient wherever you need it.
```

In a console app or test, build a provider once and resolve the client:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SolSharp.Rpc;

var provider = new ServiceCollection()
    .AddSolanaRpc("https://api.mainnet-beta.solana.com")
    .Services
    .BuildServiceProvider();

var rpc = provider.GetRequiredService<SolanaRpcClient>();
```

The WebSocket client is standalone — construct and connect it directly:

```csharp
using SolSharp.Rpc.Streaming;

await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://api.mainnet-beta.solana.com"));
```

> The examples below assume an injected/resolved `SolanaRpcClient rpc` and, where relevant, a connected
> `SolanaWsClient ws`.

## Keys and wallets

`Keypair` is the local signer. It holds only the 32-byte seed and zeroes it on `Dispose`, so wrap it in `using`.

```csharp
using SolSharp.Wallet;
using SolSharp.Core.Primitives;

// Generate a fresh key.
using var wallet = Keypair.Generate();
Console.WriteLine(wallet.PublicKey);            // base58

// Load an existing key — Parse auto-detects the format:
using var fromIdJson  = Keypair.Parse(File.ReadAllText("id.json")); // solana-keygen JSON array
using var fromPhantom = Keypair.Parse(base58Export);                // wallet export (base58)
using var fromHex     = Keypair.Parse("0x9d61b19d…");               // hex, 0x optional
using var fromBase64  = Keypair.Parse("nWGxne/9WmC…");              // base64

// Or be explicit about the format:
using var k1 = Keypair.FromBase58String(base58Export);
using var k2 = Keypair.FromSecretKey(sixtyFourBytes);  // 32-byte seed + 32-byte public key
```

Import a wallet from a BIP-39 mnemonic. Two schemes exist in the wild — pick the one your source wallet
uses:

```csharp
// solana-keygen style (no derivation path):
using var cli = Keypair.FromMnemonic("abandon abandon … about");

// Phantom / Solflare style (SLIP-0010, m/44'/501'/account'/0'):
using var account0 = Keypair.FromMnemonicAtPath("abandon abandon … about", "m/44'/501'/0'/0'");
using var account1 = Keypair.FromMnemonicAtPath("abandon abandon … about", "m/44'/501'/1'/0'");
```

The building blocks are public too: `Bip39.ToSeed(mnemonic, passphrase)` and
`Slip10.DeriveEd25519(seed, path)`.

Sign and verify:

```csharp
byte[] message = System.Text.Encoding.UTF8.GetBytes("hello");
byte[] signature = wallet.Sign(message);

bool ok = wallet.PublicKey.Verify(message, signature);   // Verify lives in SolSharp.Wallet
```

Public keys on their own:

```csharp
var mint = PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
if (PublicKey.TryParse(userInput, out var key))
    Console.WriteLine(key);

byte[] raw = mint.ToBytes();   // 32 bytes
```

## SOL and lamports

```csharp
using SolSharp.Core;

ulong lamports = SolanaUnits.SolToLamports(1.5m);     // 1_500_000_000
decimal sol    = SolanaUnits.LamportsToSol(2_000_000_000);  // 2.0
ulong perSol   = SolanaUnits.LamportsPerSol;          // 1_000_000_000
```

## Reading accounts

```csharp
var account = PublicKey.Parse("…");

ulong lamports = await rpc.GetBalanceAsync(account);

var info = await rpc.GetAccountInfoAsync(account);
if (info is not null)
{
    Console.WriteLine($"owner:    {info.Owner}");
    Console.WriteLine($"lamports: {info.Lamports}");
    Console.WriteLine($"data:     {info.Data.Length} bytes"); // already base64-decoded
}

// Several at once (order preserved; missing accounts come back null):
IReadOnlyList<AccountInfo?> many = await rpc.GetMultipleAccountsAsync([accountA, accountB]);

// Fetch only a slice of a large account (e.g. the first 8 bytes, an Anchor discriminator):
var head = await rpc.GetAccountInfoAsync(account, dataSlice: new DataSlice(0, 8));
```

`GetProgramAccountsAsync` scans every account a program owns, narrowed by memcmp / data-size filters, and
takes the same `DataSlice` (via `GetProgramAccountsOptions.DataSlice`) to trim large result sets:

```csharp
var accounts = await rpc.GetProgramAccountsAsync(
    programId,
    new GetProgramAccountsOptions { Filters = [AccountFilter.DataSize(165)] });
```

For a program that uses Anchor / Borsh layout, pair `getAccountInfo` with Core's `BorshReader`:

```csharp
using SolSharp.Core.Encoding;

var info = await rpc.GetAccountInfoAsync(account)
    ?? throw new InvalidOperationException("account not found");

var reader = new BorshReader(info.Data);
reader.Skip(8);                       // Anchor 8-byte discriminator
ulong authority = reader.ReadU64();
PublicKey owner = reader.ReadPublicKey();
bool initialized = reader.ReadBool();
```

`BorshWriter` is the inverse — build Anchor / Borsh instruction data (an 8-byte discriminator, then the args):

```csharp
var writer = new BorshWriter();
writer.WriteBytes(discriminator);     // 8-byte Anchor method discriminator
writer.WriteU64(amount);
writer.WriteOption(true);
writer.WritePublicKey(authority);
byte[] data = writer.ToArray();       // feed to new Instruction { ..., Data = data }
```

## SPL token accounts and mints

SolSharp decodes the SPL Token `Pack` layout into typed records.

```csharp
var usdc = PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

var mint = await rpc.GetMintAsync(usdc);
if (mint is not null)
{
    Console.WriteLine($"decimals: {mint.Decimals}");
    Console.WriteLine($"supply:   {mint.Supply}");
    Console.WriteLine($"mintAuthority: {mint.MintAuthority}");   // null if fixed supply
}

// A specific token account:
var tokenAccount = await rpc.GetTokenAccountAsync(someTokenAccount);
if (tokenAccount is not null)
{
    Console.WriteLine($"owner:  {tokenAccount.Owner}");
    Console.WriteLine($"mint:   {tokenAccount.Mint}");
    Console.WriteLine($"amount: {tokenAccount.Amount}");   // base units
    Console.WriteLine($"frozen: {tokenAccount.IsFrozen}");
}

// All of an owner's accounts for a given mint:
var owned = await rpc.GetTokenAccountsByOwnerAsync(owner, usdc);
foreach (var entry in owned)
{
    var decoded = TokenAccount.Decode(entry.Account.Data);  // SolSharp.Rpc.Models
    Console.WriteLine($"{entry.PublicKey}: {decoded!.Amount}");
}

// The mint's total supply as a UI amount:
var supply = await rpc.GetTokenSupplyAsync(usdc);
Console.WriteLine($"{supply.UiAmountString} ({supply.Decimals} decimals)");
```

## Sending your first transaction

Transfer SOL end-to-end: fetch a blockhash, build, sign, send, and wait for confirmation.

```csharp
using SolSharp.Core;
using SolSharp.Core.Primitives;
using SolSharp.Programs;
using SolSharp.Wallet;

using var payer = Keypair.Parse(secret);
var recipient = PublicKey.Parse("…");

var blockhash = (await rpc.GetLatestBlockhashAsync()).Blockhash;

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, SolanaUnits.SolToLamports(0.01m)))
    .Build(payer);   // the first signer becomes the fee payer unless SetFeePayer was called

// Send and wait; throws TransactionFailedException if it lands but errors on-chain.
string signature = await rpc.SendAndConfirmTransactionAsync(tx.Serialize());
Console.WriteLine(signature);
```

Fire-and-forget instead of waiting:

```csharp
string signature = await rpc.SendTransactionAsync(tx.Serialize());
```

Need devnet test funds first?

```csharp
await rpc.RequestAirdropAsync(payer.PublicKey, SolanaUnits.LamportsPerSol);
```

## Simulating before sending

Dry-run a transaction to read its logs and compute-unit cost without paying a fee.

```csharp
var sim = await rpc.SimulateTransactionAsync(tx.Serialize());

Console.WriteLine($"compute units: {sim.UnitsConsumed}");
foreach (var line in sim.Logs ?? [])
    Console.WriteLine(line);

if (sim.IsError)
    Console.WriteLine($"would fail: {sim.Err}");
```

## Priority fees (compute budget)

`ComputeBudgetProgram.SetPriorityFee` returns the unit-limit and unit-price instructions together; add them
alongside your other instructions.

```csharp
var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstructions(ComputeBudgetProgram.SetPriorityFee(
        computeUnitLimit: 200_000,
        microLamportsPerComputeUnit: 50_000))
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .Build(payer);
```

Or set the two knobs individually:

```csharp
.AddInstruction(ComputeBudgetProgram.SetComputeUnitLimit(200_000))
.AddInstruction(ComputeBudgetProgram.SetComputeUnitPrice(50_000)) // micro-lamports per CU
```

Two more compute-budget knobs exist: `RequestHeapFrame(bytes)` requests a larger transaction heap (a
multiple of 1024, up to 256 KiB), and `SetLoadedAccountsDataSizeLimit(bytes)` caps the account data the
transaction may load, lowering its loaded-accounts cost.

## SPL token transfers

Token balances live in associated token accounts (ATAs). Derive them, optionally create the recipient's,
then transfer with `TransferChecked` (which verifies mint and decimals on-chain).

```csharp
var mint = PublicKey.Parse("…");
byte decimals = 6;

var source = AssociatedTokenAccount.GetAddress(payer.PublicKey, mint);
var destination = AssociatedTokenAccount.GetAddress(recipient, mint);

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    // Create the recipient's ATA if it does not exist yet - a no-op when it already does.
    // (Plain Create would fail the transaction on an existing account.)
    .AddInstruction(AssociatedTokenAccount.CreateIdempotent(payer.PublicKey, recipient, mint))
    .AddInstruction(TokenProgram.TransferChecked(source, mint, destination, payer.PublicKey, 1_000_000, decimals))
    .Build(payer);

await rpc.SendAndConfirmTransactionAsync(tx.Serialize());
```

The full op set is available: `Transfer` / `TransferChecked`, `MintTo` / `MintToChecked`,
`Burn` / `BurnChecked`, `Approve` / `ApproveChecked`, `Revoke`, `SetAuthority` (pick the authority with
`AuthorityType`; pass no new authority to remove it permanently), `FreezeAccount` / `ThawAccount`,
`InitializeMint`, `InitializeAccount`, `CloseAccount`, `SyncNative` — plus `AssociatedTokenAccount.Create`
and `CreateIdempotent`.

```csharp
TokenProgram.MintTo(mint, destination, mintAuthority, amount: 500_000);
TokenProgram.Burn(tokenAccount, mint, owner, amount: 100_000);
```

Every builder takes an optional `tokenProgram` to target **Token-2022** (the instruction layouts are shared):

```csharp
using SolSharp.Core.Constants;

var token2022 = PublicKey.Parse(SolanaProgramIds.Token2022Program);

var ix = TokenProgram.TransferChecked(source, mint, destination, owner, 1_000_000, decimals, token2022);
var ata = AssociatedTokenAccount.GetAddress(owner, mint, token2022);  // matching ATA derivation
```

## Attaching a memo

```csharp
var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .AddInstruction(MemoProgram.Memo("gm", payer.PublicKey))  // signer(s) optional
    .Build(payer);
```

## Versioned (v0) transactions and address lookup tables

A v0 transaction can load extra accounts from an on-chain Address Lookup Table (ALT) instead of listing
them all in the message. Fetch the table, wrap it, hand it to the builder, and call `BuildV0`.

```csharp
using SolSharp.Programs;

var tableKey = PublicKey.Parse("…");

// Fetch + decode the table (SolSharp.Rpc model), then wrap it for the builder.
var fetched = await rpc.GetAddressLookupTableAsync(tableKey)
    ?? throw new InvalidOperationException("lookup table not found");
var table = new AddressLookupTableAccount(tableKey, fetched.Addresses);

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .SetAddressLookupTables(table)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .BuildV0(payer);

await rpc.SendTransactionAsync(tx.Serialize());
```

Accounts that appear in the table (and are not signers or program IDs) are drained out of the static keys
and referenced through the table, shrinking the transaction. Building the table itself is done with
`AddressLookupTableProgram` (`CreateLookupTable`, `ExtendLookupTable`, `DeactivateLookupTable`, `CloseLookupTable`).

## Decoding a transaction

Parse a serialized transaction (from `getTransaction`, a log, or a wallet) back into a `Transaction`.

```csharp
using SolSharp.Programs;

byte[] raw = Convert.FromBase64String(base64Tx);
var tx = Transaction.Deserialize(raw);

Console.WriteLine(tx.Message is MessageV0 ? "versioned (v0)" : "legacy");
Console.WriteLine($"required signers: {tx.Message.RequiredSignatures}");
foreach (var key in tx.Message.AccountKeys)
    Console.WriteLine(key);
```

You can re-sign a parsed transaction (for example to add your signature to a partially signed one): only the
matching signer's slot is filled, leaving existing signatures intact.

```csharp
tx.Sign(payer);
string resubmittable = tx.ToBase64();
```

### Analyzing a historical transaction

`getTransaction` returns the decoded bytes plus rich metadata. Parse the bytes, **decompile** the instructions
(resolving each account index to a public key and signer/writable flags), read the token-balance deltas, and
decode any failure into a typed error:

```csharp
var fetched = await rpc.GetTransactionAsync(signature);
if (fetched is not null)
{
    var parsed = Transaction.Deserialize(fetched.Transaction!);

    // Resolve instructions to program ids + account keys. A v0 transaction loads accounts from lookup tables:
    var instructions = parsed.Message is MessageV0 v0
        ? v0.DecompileInstructions(await FetchTablesAsync(rpc, v0))
        : parsed.Message.DecompileInstructions();

    foreach (var ix in instructions)
        Console.WriteLine($"{ix.ProgramId} over {ix.Accounts.Count} accounts");

    foreach (var post in fetched.Meta?.PostTokenBalances ?? [])
        Console.WriteLine($"{post.Mint}: {post.UiTokenAmount.UiAmountString}");

    if (fetched.Meta?.Error is { } error)   // typed failure reason
        Console.WriteLine(error.InstructionError?.CustomCode is { } code
            ? $"failed with program error {code}"
            : error.ToString());
}

// Fetch and wrap the lookup tables a v0 message references:
static async Task<IReadOnlyList<AddressLookupTableAccount>> FetchTablesAsync(SolanaRpcClient rpc, MessageV0 message)
{
    var tables = new List<AddressLookupTableAccount>();
    foreach (var lookup in message.AddressTableLookups)
    {
        var table = await rpc.GetAddressLookupTableAsync(lookup.AccountKey)
            ?? throw new InvalidOperationException($"lookup table {lookup.AccountKey} not found");
        tables.Add(new AddressLookupTableAccount(lookup.AccountKey, table.Addresses));
    }
    return tables;
}
```

`MessageV0.GetAccountKeys(tables)` gives the full resolved account list (static + lookup-loaded), so you can
map a balance entry's `accountIndex` back to a public key.

## Reading parsed transactions

When you'd rather not Borsh-decode instructions yourself, ask the node to do it: the `jsonParsed` encoding
returns recognized instructions, token balances and logs already decoded. SolSharp exposes this as a separate
read path that sits alongside the raw one. Every instruction keeps both forms — a typed `Parsed` view when the
node recognizes the program, and the raw `ProgramId` / `Accounts` / `Data` when it doesn't — so nothing is lost.

```csharp
var tx = await rpc.GetParsedTransactionAsync(signature);
if (tx is not null)
{
    foreach (var ix in tx.Message.Instructions)
    {
        if (ix.Parsed is { } parsed)
            Console.WriteLine($"{ix.Program} {parsed.Type}");        // recognized: typed action + decoded fields
        else
            Console.WriteLine($"{ix.ProgramId} over {ix.Accounts?.Count ?? 0} accounts");  // unrecognized: raw
    }

    foreach (var balance in tx.Meta?.PostTokenBalances ?? [])         // token balances, already decoded
        Console.WriteLine($"{balance.Owner} holds {balance.UiTokenAmount.UiAmountString} of {balance.Mint}");

    foreach (var log in tx.Meta?.LogMessages ?? [])
        Console.WriteLine(log);
}
```

`Parsed.Info` is a `JsonElement`, so you read whatever fields the specific instruction type carries:

```csharp
var transfer = tx.Message.Instructions.First(ix => ix.Parsed?.Type == "transfer");
ulong lamports = transfer.Parsed!.Info.GetProperty("lamports").GetUInt64();
```

`GetParsedBlockAsync(slot)` returns a whole block of parsed transactions (each with its `Slot` and `BlockTime`
filled in); over the WebSocket, `SubscribeParsedBlocksAsync` streams the same parsed blocks. As with the raw
path, `GetParsedTransactionAsync` returns `null` when the signature isn't found and `GetParsedBlockAsync`
returns `null` for a skipped slot.

The same `jsonParsed` encoding decodes **account** state too: `GetParsedAccountInfoAsync` returns the node's
typed view of a recognized account (an SPL token account or mint, a stake account, …) and falls back to raw
bytes when the owning program is unknown. `SubscribeParsedAccountAsync` streams that same parsed view over the
WebSocket.

```csharp
var account = await rpc.GetParsedAccountInfoAsync(usdcMint);
if (account?.Parsed is { } parsed)
    Console.WriteLine($"{account.Program} {parsed.Type}");                  // recognized, e.g. "spl-token" "mint"
else if (account is not null)
    Console.WriteLine($"{account.Owner}: {account.RawData?.Length ?? 0} raw bytes"); // unrecognized program
```

## Cluster and validator info

Beyond accounts and transactions, the client reads the cluster's own state:

```csharp
var epoch = await rpc.GetEpochInfoAsync();                  // current epoch + slot progress
var votes = await rpc.GetVoteAccountsAsync();               // active + delinquent validators
var schedule = await rpc.GetLeaderScheduleAsync();          // leader slots by validator (current epoch)
var nodes = await rpc.GetClusterNodesAsync();               // gossip / TPU / RPC addresses + versions
var blocks = await rpc.GetBlocksAsync(startSlot, endSlot);  // confirmed slots in a range

// Staking rewards paid to a set of addresses for a given epoch (null per address when there were none):
var rewards = await rpc.GetInflationRewardAsync([voteAccount], epoch: 600);
```

## WebSocket subscriptions

All subscriptions share one connection and survive dropped connections (auto-reconnect + resubscribe).
Slots arrive as an `IAsyncEnumerable`; the rest return a `ChannelReader`.

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;

await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://api.mainnet-beta.solana.com"));

// Slots:
await foreach (var slot in ws.SubscribeSlotsAsync())
    Console.WriteLine(slot.Slot);

// Logs mentioning a program (ChannelReader):
var logs = await ws.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
await foreach (var note in logs.ReadAllAsync())
    Console.WriteLine(note.Value!.Signature);

// Account changes:
var accounts = await ws.SubscribeAccountAsync(someAccount);
await foreach (var note in accounts.ReadAllAsync())
    Console.WriteLine(note.Value!.Lamports);
```

Also available: `SubscribeRootsAsync` (rooted slots, like `SubscribeSlotsAsync`), `SubscribeProgramAsync`
(with memcmp / data-size filters), `SubscribeSignatureAsync`, `SubscribeBlocksAsync`, and the `jsonParsed`
streams `SubscribeParsedBlocksAsync` / `SubscribeParsedAccountAsync`. Cancel any channel subscription by
cancelling the `CancellationToken` you pass in.

The reconnect policy is tunable through `SolanaWsClientOptions`: `AutoReconnect` (on by default), the
`ReconnectInitialDelay` → `ReconnectMaxDelay` exponential backoff, and `MaxReconnectAttempts` (`0` retries
forever). When the attempts are exhausted — or auto-reconnect is off — every subscription completes with the
connection error. Failure semantics are per-subscription otherwise: a subscribe the node rejects throws
`InvalidOperationException` carrying the node's error code and message, and a notification that fails to
decode faults only its own subscription while the connection and the other subscriptions keep going.
Disposing the client completes every channel and stream.

## Confirming a transaction

Two ways to wait for a signature to reach a commitment level — poll, or get pushed over the WebSocket.

```csharp
// Poll getSignatureStatuses until confirmed:
var status = await rpc.ConfirmTransactionAsync(signature);
Console.WriteLine(status.ConfirmationStatus);

// Or wait for a single push over the WebSocket (no polling):
var result = await ws.ConfirmSignatureAsync(signature);
if (result.IsError)
    Console.WriteLine("transaction failed on-chain");
```

`SendAndConfirmTransactionAsync` wraps the send-then-poll flow and throws `TransactionFailedException` if the
transaction lands but errors.

## Durable nonces

A blockhash expires after roughly a minute; a durable nonce lets a transaction be signed now and submitted
later. Create the nonce account once, then anchor transactions to its current nonce value.

```csharp
using SolSharp.Programs;

// One-time setup: create + initialize the nonce account (80 bytes, rent-exempt).
using var nonceKeypair = Keypair.Generate();
var rent = await rpc.GetMinimumBalanceForRentExemptionAsync(SystemProgram.NonceAccountLength);
var setup = new TransactionBuilder()
    .SetRecentBlockhash((await rpc.GetLatestBlockhashAsync()).Blockhash)
    .AddInstructions(SystemProgram.CreateNonceAccount(payer.PublicKey, nonceKeypair.PublicKey, payer.PublicKey, rent))
    .Build(payer, nonceKeypair);
await rpc.SendAndConfirmTransactionAsync(setup.Serialize());

// Later — sign a transaction that stays valid until the nonce is advanced:
var nonce = await rpc.GetNonceAccountAsync(nonceKeypair.PublicKey)
    ?? throw new InvalidOperationException("nonce account not found");

var tx = new TransactionBuilder()
    .SetDurableNonce(nonceKeypair.PublicKey, payer.PublicKey, nonce.Nonce) // prepends AdvanceNonceAccount
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .Build(payer);

await rpc.SendTransactionAsync(tx.Serialize());
```

`SetDurableNonce` uses the nonce value as the recent blockhash and prepends the required
`AdvanceNonceAccount` instruction, so each submission consumes the nonce exactly once.

## Program-derived addresses (PDAs)

```csharp
using System.Text;
using SolSharp.Programs;
using SolSharp.Wallet;   // IsOnCurve

var (pda, bump) = ProgramDerivedAddress.FindProgramAddress(
    [Encoding.UTF8.GetBytes("vault"), owner.ToBytes()],
    programId);

// Check whether a key is a valid Ed25519 point (PDAs are off-curve):
bool onCurve = somePublicKey.IsOnCurve();
```

A derivation accepts at most `MaxSeeds` (16) seeds — the bump counts toward the limit — of up to
`MaxSeedLength` (32) bytes each, matching the runtime's rules.

## Rate limits, custom endpoints, and headers

`AddSolanaRpc` takes an options delegate and an optional resilience delegate; the returned builder is a
standard `IHttpClientBuilder`, so you can add headers or swap the handler.

```csharp
using Microsoft.Extensions.DependencyInjection;
using SolSharp.Rpc;

services.AddSolanaRpc(
        options => options.Endpoint = "https://your-node.example/<token>",
        resilience =>
        {
            resilience.Retry.MaxRetryAttempts = 5;          // back off harder on a busy provider
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        })
    .ConfigureHttpClient(http =>
        http.DefaultRequestHeaders.Add("x-api-key", apiKey)); // auth header for the provider
```

## Error handling

- **`RpcException`** — the node returned a JSON-RPC error; `Code` and `Message` carry the details.
- **`TransactionFailedException`** — from `SendAndConfirmTransactionAsync` when the transaction is confirmed
  but errored on-chain; `Signature` and the error payload are attached.
- **`HttpRequestException`** — a transport-level failure or non-success status (after the resilience pipeline
  has exhausted its retries).
- **`FormatException`** — malformed input to a parser (`PublicKey.Parse`, `Keypair.Parse`, `Transaction.Deserialize`).

```csharp
try
{
    var signature = await rpc.SendAndConfirmTransactionAsync(tx.Serialize());
}
catch (TransactionFailedException ex)
{
    Console.WriteLine($"{ex.Signature} failed on-chain");
}
catch (RpcException ex)
{
    Console.WriteLine($"node rejected the request: {ex.Code} {ex.Message}");
}
```
