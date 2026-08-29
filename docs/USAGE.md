# SolSharp — Usage Guide

A task-oriented tour of SolSharp with copy-pasteable C# examples. For the high-level overview and design
notes see the [README](../README.md); for conventions and architecture see [AGENTS.md](../AGENTS.md).

Every snippet targets **.NET 10** and uses the single `SolSharp` NuGet package, which bundles four
functional assemblies plus a minimal packaging facade — the namespaces `SolSharp.Core.*`, `SolSharp.Rpc`,
`SolSharp.Wallet`, and `SolSharp.Programs`.
Unless a snippet shows a narrower import list, start with this common preamble:

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Core.SysvarStates;
using SolSharp.Programs;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Protocol;
using SolSharp.Rpc.Streaming;
using SolSharp.Wallet;
```

## Contents

- [Installation](#installation)
- [Creating a client](#creating-a-client)
- [Keys and wallets](#keys-and-wallets)
- [Signed off-chain messages](#signed-off-chain-messages)
- [SOL and lamports](#sol-and-lamports)
- [Reading accounts](#reading-accounts)
- [SPL token accounts and mints](#spl-token-accounts-and-mints)
- [Sending your first transaction](#sending-your-first-transaction)
- [Simulating before sending](#simulating-before-sending)
- [Priority fees (compute budget)](#priority-fees-compute-budget)
- [SPL token transfers](#spl-token-transfers)
- [Advanced Token-2022 interfaces](#advanced-token-2022-interfaces)
- [Attaching a memo](#attaching-a-memo)
- [Native programs and account state](#native-programs-and-account-state)
- [Versioned (v0) transactions and address lookup tables](#versioned-v0-transactions-and-address-lookup-tables)
- [SIMD-0385 V1 transactions](#simd-0385-v1-transactions)
- [Decoding a transaction](#decoding-a-transaction)
- [Reading parsed transactions](#reading-parsed-transactions)
- [Cluster and validator info](#cluster-and-validator-info)
- [WebSocket subscriptions](#websocket-subscriptions)
- [Confirming a transaction](#confirming-a-transaction)
- [Durable nonces](#durable-nonces)
- [Program-derived addresses (PDAs)](#program-derived-addresses-pdas)
- [Batching RPC calls](#batching-rpc-calls)
- [Allocation-free serialization](#allocation-free-serialization)
- [Rate limits, custom endpoints, and headers](#rate-limits-custom-endpoints-and-headers)
- [Error handling](#error-handling)
- [Publishing with Native AOT](#publishing-with-native-aot)

## Installation

SolSharp ships as one NuGet package that bundles four functional assemblies plus a minimal packaging facade:

```bash
dotnet add package SolSharp
```

That single reference brings in every namespace — `SolSharp.Core.*`, `SolSharp.Rpc`, `SolSharp.Wallet`, and
`SolSharp.Programs` — so there's no juggling which project to add. Requires .NET 10 or later.

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

The WebSocket client can be constructed directly:

```csharp
using SolSharp.Rpc.Streaming;

await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://api.mainnet-beta.solana.com"));
```

Or let the container manage it: `AddSolanaWs` registers `SolanaWsClient` as a singleton wired to the
registered `ILoggerFactory` and disposes it on shutdown. You still open the connection yourself:

```csharp
services.AddSolanaWs(new SolanaWsClientOptions
{
    MaxReconnectAttempts = 10,
    ReceiveTimeout = TimeSpan.FromMinutes(2),   // opt-in: only for high-frequency subscriptions
    MaxMessageSizeBytes = 64 * 1024 * 1024,
    SubscriptionBufferCapacity = 1024,
    MaxBufferedNotificationBytesPerSubscription = 64L * 1024 * 1024,
    MaxBufferedNotificationBytesTotal = 256L * 1024 * 1024,
    MaxPendingSubscriptionRequests = 1024
});

var ws = provider.GetRequiredService<SolanaWsClient>();
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

// Or be explicit about the format — each string form takes a 32-byte seed or a 64-byte secret key:
using var k1 = Keypair.FromBase58String(base58Export);
using var k2 = Keypair.FromSecretKey(sixtyFourBytes);   // 32-byte seed + 32-byte public key
using var k3 = Keypair.FromJsonArray(idJsonText);       // the solana-keygen id.json array
using var k4 = Keypair.FromHexString("0x9d61b19d…");    // 0x optional
using var k5 = Keypair.FromBase64String(base64Secret);
using var k6 = Keypair.FromSeed(thirtyTwoBytes);        // just the 32-byte seed
```

Export only when another trusted tool needs the secret. `ToBytes` returns the Solana/Rust SDK
64-byte form (seed followed by public key), while `ToJsonArray` matches `solana-keygen id.json` and
`ToBase58String` matches common wallet exports. Byte arrays can and should be cleared; strings cannot:

```csharp
using System.Security.Cryptography;
using System.Text;

byte[] secretKey = wallet.ToBytes();
try
{
    // Keep plaintext key files outside the repository and restrict them to the current user.
    var keyDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "solana");
    Directory.CreateDirectory(keyDirectory);
    var keyPath = Path.Combine(keyDirectory, "id.json");
    var fileOptions = new FileStreamOptions
    {
        Mode = FileMode.CreateNew, // refuses to overwrite a file or follow an existing id.json symlink
        Access = FileAccess.Write,
        Share = FileShare.None
    };
    if (!OperatingSystem.IsWindows())
        fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    using (var keyFile = new FileStream(keyPath, fileOptions))
    using (var writer = new StreamWriter(keyFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        writer.Write(wallet.ToJsonArray());

    using var imported = Keypair.FromSecretKey(secretKey);
    Console.WriteLine(imported.PublicKey == wallet.PublicKey);
}
finally
{
    CryptographicOperations.ZeroMemory(secretKey);
}
```

`ToSeedBytes` exports only the 32-byte Ed25519 seed. Prefer byte APIs over the base58/JSON string
forms whenever possible, and never log or commit any exported value.

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
`Slip10.DeriveEd25519(seed, path)`. SLIP-0010 path segments use canonical ASCII digits followed by
`'`; signs, whitespace, and non-hardened segments are rejected.

Sign and verify with either the compatibility byte-array API or the typed 64-byte `Signature` value:

```csharp
byte[] message = System.Text.Encoding.UTF8.GetBytes("hello");
byte[] signatureBytes = wallet.Sign(message);
Signature signature = wallet.SignSignature(message);

bool ok = signature.Verify(wallet.PublicKey, message);   // Wallet's Ed25519 verification policy
bool same = wallet.PublicKey.Verify(message, signature); // equivalent Wallet extension
```

Verification uses Bouncy Castle plus an additional small-order signature-point rejection. It interoperates
for ordinary signatures but is not a consensus-acceptance predicate for pathological point encodings; the
exact pinned `verify_dalek` difference is recorded in `docs/RUST_PARITY.md`. `Signature.Parse` / `TryParse`
use the base58 form returned by Solana RPC, while `ToBytes` and `CopyTo` expose its exact 64 bytes.

Current Vote v2/v4 contracts use the pinned minimal-public-key-size BLS12-381 proof-of-possession
scheme. Derive the BLS key from high-entropy input key material (or from an existing signer), bind its
proof to the vote account, and pass only validated typed values to the Vote builder:

```csharp
using SolSharp.Programs;
using SolSharp.Wallet;

using var bls = BlsKeypair.Derive(blsInputKeyMaterial); // at least 32 high-entropy bytes
BlsProofOfPossession proof = bls.CreateVoteProofOfPossession(voteAccount);

var initialize = new VoteInitializeV2(
    node,
    authorizedVoter,
    bls.PublicKey,
    proof,
    authorizedWithdrawer,
    inflationRewardsCommissionBps: 500,
    blockRevenueCommissionBps: 500);

Instruction initializeVote = VoteProgram.InitializeAccountV2(
    voteAccount,
    initialize,
    inflationRewardsCollector,
    blockRevenueCollector);
```

Like the pinned Rust SDK, a raw `BlsPublicKey` can validate a proof but cannot verify signatures directly.
Call `BlsKeypair.Verify` for a locally derived key, or verify the proof with
`VerifyAndWrapProofOfPossession` and call `BlsPopVerifiedPublicKey.Verify`. This keeps signer attribution and
aggregation behind an explicit proof-of-possession boundary.

Same-message aggregation requires proof-of-possession provenance before any public key can enter the
aggregate. The typed wrapper makes the rogue-key check explicit at the API boundary:

```csharp
using SolSharp.Wallet;

using var firstBlsSigner = BlsKeypair.Derive(firstInputKeyMaterial);
using var secondBlsSigner = BlsKeypair.Derive(secondInputKeyMaterial);

ReadOnlySpan<byte> registryPayload = "validator-registry"u8;
var firstVerifiedKey = firstBlsSigner.PublicKey.VerifyAndWrapProofOfPossession(
    firstBlsSigner.CreateProofOfPossession(registryPayload),
    registryPayload);
var secondVerifiedKey = secondBlsSigner.PublicKey.VerifyAndWrapProofOfPossession(
    secondBlsSigner.CreateProofOfPossession(registryPayload),
    registryPayload);

ReadOnlySpan<byte> sharedMessage = "shared vote payload"u8;
var aggregateKey = BlsAggregatePublicKey.Aggregate([firstVerifiedKey, secondVerifiedKey]);
var aggregateSignature = BlsSignature.Aggregate(
    [firstBlsSigner.Sign(sharedMessage), secondBlsSigner.Sign(sharedMessage)]);

bool aggregateIsValid = aggregateKey.Verify(aggregateSignature, sharedMessage);
```

This API is deliberately for one shared message. SolSharp does not expose the pinned SDK's
consensus-oriented distinct-message screening helper as a general application-security primitive.

`BlsKeypair.ToBytes` / `FromBytes` and `ToJsonUtf8Bytes` / `FromJsonArray(ReadOnlySpan<byte>)` use the
pinned Rust 128-byte keypair form. Both byte exports contain the secret: clear them after use. String
`ToJsonArray` / `FromJsonArray(string)` remain available for interoperability, but immutable .NET strings
cannot be zeroed and should not be the default. Compressed public keys, signatures, and proofs use strict,
fixed-length base64 text. The native BLS backend ships for `linux-x64`, `linux-arm64`, `osx-x64`,
`osx-arm64`, and `win-x64`; other RIDs must not call BLS APIs.

For an air-gapped, hardware, or remote signing flow, wrap a signature obtained elsewhere in `Presigner`.
It re-verifies the public key and exact message on every signing request, so a signature for a different
transaction cannot be attached accidentally. `NullSigner` is the matching all-zero placeholder for an
absent required signer:

```csharp
Signature externalSignature = Signature.Parse(base58SignatureFromHardwareWallet);
var external = new Presigner(externalPublicKey, externalSignature);
byte[] verifiedBytes = external.Sign(serializedMessage); // throws if key/message/signature do not match

var absent = new NullSigner(cosignerPublicKey);
byte[] placeholder = absent.Sign(serializedMessage);      // exactly 64 zero bytes
```

Public keys on their own:

```csharp
var mint = PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
if (PublicKey.TryParse(userInput, out var key))
    Console.WriteLine(key);

byte[] raw = mint.ToBytes();   // 32 bytes
```

Blockhashes and message hashes use the distinct 32-byte `Hash` value, with the same base58 and copy APIs.
String overloads remain available for compatibility:

```csharp
Hash recentBlockhash = Hash.Parse((await rpc.GetLatestBlockhashAsync()).Blockhash);

var message = new TransactionBuilder()
    .SetRecentBlockhash(recentBlockhash)
    .SetFeePayer(wallet.PublicKey)
    .AddInstruction(SystemProgram.Transfer(wallet.PublicKey, recipient, lamports))
    .BuildMessage();
```

## Signed off-chain messages

`OffchainMessage` implements the pinned Solana SDK's version-0 domain and wire format for signing a
human-readable payload without constructing a transaction. The signed bytes are domain-separated from
transaction messages and grant no on-chain authority by themselves:

```csharp
using SolSharp.Wallet;

using var signer = Keypair.Generate();
var message = OffchainMessage.Create("Approve login for example.com");

Signature signature = message.Sign(signer);
byte[] wire = message.Serialize();

var received = OffchainMessage.Deserialize(wire);
bool authentic = received.Verify(signer.PublicKey, signature);
Console.WriteLine($"{received.Format}: {authentic}");
```

The canonical format is selected automatically: printable ASCII up to the ledger-sized limit, bounded
UTF-8 at that limit, or extended UTF-8 up to the version-0 `ushort` wire maximum. Empty payloads,
invalid UTF-8, mismatched declared lengths, unknown versions/formats, and payloads that do not satisfy
their declared format are rejected before signing or verification.

## SOL and lamports

```csharp
using SolSharp.Core;

ulong lamports = SolanaUnits.SolToLamports(1.5m);     // 1_500_000_000
decimal sol    = SolanaUnits.LamportsToSol(2_000_000_000);  // 2.0
ulong perSol   = SolanaUnits.LamportsPerSol;          // 1_000_000_000
```

## Reading accounts

```csharp
using SolSharp.Core.Primitives;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;

var account = PublicKey.Parse("…");

ulong lamports = await rpc.GetBalanceAsync(account);

var info = await rpc.GetAccountInfoAsync(account);
if (info is not null)
{
    Console.WriteLine($"owner:    {info.Owner}");
    Console.WriteLine($"lamports: {info.Lamports}");
    Console.WriteLine($"data:     {info.Data.Length} bytes"); // already base64-decoded
    Console.WriteLine($"full size: {info.Space} bytes");     // still the full size when DataSlice was used
}

// Several at once (order preserved; missing accounts come back null):
IReadOnlyList<AccountInfo?> many = await rpc.GetMultipleAccountsAsync([accountA, accountB]);

// Fetch only a slice of a large account (e.g. the first 8 bytes, an Anchor discriminator):
var head = await rpc.GetAccountInfoAsync(account, dataSlice: new DataSlice(0, 8));

// The convenient base64 path can also preserve the response context and protect the read
// from being evaluated before a known slot:
var contextual = await rpc.GetAccountInfoWithContextAsync(
    account,
    new GetAccountInfoOptions
    {
        Commitment = Commitment.Confirmed,
        DataSlice = new DataSlice(0, 8),
        MinContextSlot = lastObservedSlot
    });
Console.WriteLine($"evaluated at slot {contextual.Context.Slot}");

// The exact path preserves every upstream account-data branch, including base58,
// base64+zstd and jsonParsed (whose unknown-program fallback is a base64 tuple):
var exactResponse = await rpc.GetAccountInfoWithOptionsAndContextAsync(
    account,
    new RpcAccountInfoOptions
    {
        Encoding = RpcAccountEncoding.JsonParsed,
        Commitment = Commitment.Confirmed,
        MinContextSlot = lastObservedSlot
    });
var exact = exactResponse.Value;
Console.WriteLine($"exact branch evaluated at slot {exactResponse.Context.Slot}");

if (exact?.Data is RpcAccountData.Parsed parsed)
    Console.WriteLine($"{parsed.Program}: {parsed.Value}");
else if (exact?.Data is RpcAccountData.Encoded encoded)
    Console.WriteLine($"fallback encoding: {encoded.Encoding}");
else if (exact?.Data is RpcAccountData.LegacyBinary legacy)
    Console.WriteLine($"legacy base58: {legacy.EncodedData}");
```

Use `GetAccountInfoWithOptionsAsync` when the exact data branch matters but the context does not. The
multiple-account, program-account, owner-filter, and delegate-filter exact reads follow the same naming:
their `WithOptionsAsync` variants return values directly, while `WithOptionsAndContextAsync` preserves
the upstream `{ context, value }` wrapper.

`GetProgramAccountsAsync` scans every account a program owns, narrowed by the full upstream filter union, and
takes the same `DataSlice` (via `GetProgramAccountsOptions.DataSlice`) to trim large result sets:

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc;

var programId = PublicKey.Parse(SolanaProgramIds.TokenProgram);
var mintBytesBase58 = PublicKey.Parse(Mints.WrappedSol).ToString();
var ownerBytesBase64 = Convert.ToBase64String(
    PublicKey.Parse(SolanaProgramIds.SystemProgram).ToBytes());

var accounts = await rpc.GetProgramAccountsAsync(
    programId,
    new GetProgramAccountsOptions
    {
        Filters =
        [
            AccountFilter.DataSize(165),
            AccountFilter.MemoryCompareBase58(0, mintBytesBase58),
            AccountFilter.MemoryCompareBase64(32, ownerBytesBase64),
            AccountFilter.TokenAccountState()
        ]
    });

// Preserve the response context and ask the node to return deterministic balance ordering:
var contextualAccounts = await rpc.GetProgramAccountsWithContextAsync(
    programId,
    new GetProgramAccountsOptions
    {
        Filters = [AccountFilter.DataSize(165)],
        DataSlice = new DataSlice(0, 32),
        MinContextSlot = lastObservedSlot,
        SortResults = true
    });
```

For a program that uses Anchor / Borsh layout, pair `getAccountInfo` with Core's `BorshReader`:

```csharp
using SolSharp.Core.Encoding;

var info = await rpc.GetAccountInfoAsync(account)
    ?? throw new InvalidOperationException("account not found");
var (authority, owner, initialized) = DecodeAccount(info.Data);

static (ulong Authority, PublicKey Owner, bool Initialized) DecodeAccount(ReadOnlySpan<byte> data)
{
    var reader = new BorshReader(data);
    reader.Skip(8);                   // Anchor 8-byte discriminator
    return (reader.ReadU64(), reader.ReadPublicKey(), reader.ReadBool());
}
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
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;

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

// Same shape for accounts approved to a delegate:
var delegated = await rpc.GetTokenAccountsByDelegateAsync(delegateKey, usdc);

// Or scan every classic-Token account owned by the wallet, without fixing one mint:
var everyClassicTokenAccount = await rpc.GetTokenAccountsByOwnerWithFilterAsync(
    owner,
    TokenAccountsFilter.ByProgramId(PublicKey.Parse(SolanaProgramIds.TokenProgram)));

// The mint's total supply as a UI amount:
var supply = await rpc.GetTokenSupplyAsync(usdc);
Console.WriteLine($"{supply.UiAmountString} ({supply.Decimals} decimals)");

// One account's balance without decoding it, and a mint's 20 largest holders:
var balance = await rpc.GetTokenAccountBalanceAsync(someTokenAccount);
var holders = await rpc.GetTokenLargestAccountsAsync(usdc);
foreach (var holder in holders)
    Console.WriteLine($"{holder.Address}: {holder.UiAmountString}");
```

**Token-2022 extensions.** An extended Token-2022 mint or account carries TLV entries after its base
layout. Decode them alongside the base state:

```csharp
using SolSharp.Rpc.Models.Token2022;

var info = await rpc.GetAccountInfoAsync(mintAddress)
    ?? throw new InvalidOperationException("mint not found");

var mint = Mint.Decode(info.Data);                        // the base 82-byte state
var extensions = TokenExtensionSet.DecodeMint(info.Data); // the TLV extension section

if (extensions?.GetTransferFeeConfig() is { } fee)
    Console.WriteLine($"transfer fee: {fee.GetEpochFee(currentEpoch).BasisPoints} bps");

var symbol = extensions?.GetTokenMetadata()?.Symbol;      // in-mint metadata, when present
```

Typed getters cover the common extensions — transfer-fee config and withheld amounts, metadata pointer and
in-mint `TokenMetadata`, permanent delegate, mint close authority, default account state, memo-transfer
requirement. Every other TLV entry stays available raw via `Extensions` and `Find(type)`.

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

`SendTransactionOptions` tunes the submission (`SkipPreflight`, `PreflightCommitment`, `MaxRetries`,
`MinContextSlot`). Preflight runs at `confirmed` by default, matching `GetLatestBlockhashAsync` — on the
node's own `finalized` default a just-fetched blockhash may not exist yet, and preflight would report
`BlockhashNotFound` for a perfectly valid transaction.

Need devnet test funds first?

```csharp
await rpc.RequestAirdropWithOptionsAsync(
    payer.PublicKey,
    SolanaUnits.LamportsPerSol,
    new RequestAirdropOptions
    {
        Commitment = Commitment.Confirmed,
        RecentBlockhash = blockhash
    });
```

`SystemProgram` covers more than transfers: `CreateAccount(from, newAccount, lamports, space, owner)`,
`CreateAccountAllowPrefund` for an already funded destination, `TransferMany` for composable fan-out,
`Allocate` / `Assign`, the seed-derived variants (`CreateAccountWithSeed`, `AllocateWithSeed`,
`AssignWithSeed`, `TransferWithSeed`), and the durable-nonce instruction set
(see [Durable nonces](#durable-nonces)).

## Simulating before sending

Dry-run a transaction to read its logs and compute-unit cost without paying a fee.

```csharp
var sim = await rpc.SimulateTransactionAsync(
    tx.Serialize(),
    new SimulateTransactionOptions
    {
        Accounts = [payer.PublicKey],
        AccountsEncoding = RpcAccountEncoding.JsonParsed,
        InnerInstructions = true
    });

Console.WriteLine($"compute units: {sim.UnitsConsumed}");
foreach (var line in sim.Logs ?? [])
    Console.WriteLine(line);

if (sim.IsError)
    Console.WriteLine($"would fail: {sim.Err}");

Console.WriteLine($"fee: {sim.Fee}; loaded bytes: {sim.LoadedAccountsDataSize}");
Console.WriteLine($"returned accounts: {sim.Accounts?.Count ?? 0}");
Console.WriteLine($"CPI groups: {sim.InnerInstructions?.Count ?? 0}");
```

`SimulateTransactionOptions` controls the run (`SigVerify`, `ReplaceRecentBlockhash`, `Commitment`,
`MinContextSlot`, `Accounts`, `AccountsEncoding`, `InnerInstructions`). Account snapshots preserve the
node's exact base64, base64+zstd, or jsonParsed branch through `RpcAccountData`; like preflight, the simulation runs at `confirmed` by
default so a blockhash fetched with `GetLatestBlockhashAsync` is visible to it. When the node reports them,
the result also preserves replacement blockhashes, pre/post SOL and token balances, loaded addresses,
program return data, and cost details.

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

Pick the price from data instead of guessing — recent per-slot priority fees, narrowed to the accounts
your transaction locks — and check the base fee of a compiled message:

```csharp
var fees = await rpc.GetRecentPrioritizationFeesAsync([payer.PublicKey, recipient]);
var suggested = fees.Max(f => f.Fee);       // micro-lamports per CU; take a max/percentile over ~150 slots

var baseFee = await rpc.GetFeeForMessageAsync(tx.Message.Serialize());  // lamports, null for an unknown blockhash
```

To price a transaction **before** signing it, compile just the message — `BuildMessage` / `BuildMessageV0`
skip the signers (set the fee payer explicitly):

```csharp
var message = new TransactionBuilder()
    .SetFeePayer(payer.PublicKey)
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .BuildMessage();               // or BuildMessageV0() with lookup tables set

var fee = await rpc.GetFeeForMessageAsync(message.Serialize());
```

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

The supported builders include `Transfer` / `TransferChecked`, `MintTo` / `MintToChecked`,
`Burn` / `BurnChecked`, `Approve` / `ApproveChecked`, `Revoke`, `SetAuthority` (pick the authority with
`AuthorityType`; pass no new authority to remove it permanently), `FreezeAccount` / `ThawAccount`,
`InitializeMint`, `InitializeAccount`, `CloseAccount`, `SyncNative` — plus `AssociatedTokenAccount.Create`
and `CreateIdempotent`.

Authority-bearing builders also have SPL Multisig overloads. Pass the multisig account as `authority`
and the member public keys in the same order as their signer account metas; the multisig account itself
is deliberately not marked as a signer. SPL Token permits between 1 and 11 supplied member signers:

```csharp
var transfer = TokenProgram.TransferChecked(
    source,
    mint,
    destination,
    authority: multisigAccount,
    amount: 1_000_000,
    decimals: decimals,
    tokenProgram: null,
    multisigSigners: [memberA.PublicKey, memberB.PublicKey]);

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(transfer)
    .Build(payer, memberA, memberB);
```

`AuthorityType` also carries the Token-2022 extension authorities (`TransferFeeConfig`, `CloseMint`,
`PermanentDelegate`, `MetadataPointer`, ...), valid when the instruction targets the Token-2022 program.
Passing one while targeting classic SPL Token is rejected before an invalid instruction can be built:

```csharp
using SolSharp.Core.Constants;

TokenProgram.SetAuthority(
    mint, currentAuthority, AuthorityType.TransferFeeConfig, newAuthority,
    tokenProgram: PublicKey.Parse(SolanaProgramIds.Token2022Program));
```

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

`AssociatedTokenAccount.RecoverNested` moves tokens out of an accidentally nested ATA and closes the
nested account using the canonical owner/mint derivation; its owner mint must itself be the wallet's ATA.

## Advanced Token-2022 interfaces

The pinned Token-2022 client contracts include extension allocation/initialization, transfer fees,
metadata, token groups, transfer hooks, confidential-transfer/fee/mint-burn instruction families,
native proof-program PODs, and the ElGamal registry. Group and member values have fixed typed decoders:

```csharp
Instruction growAccount = Token2022Program.Reallocate(
    tokenAccount,
    payer.PublicKey,
    owner.PublicKey,
    [Token2022ExtensionType.MemoTransfer, Token2022ExtensionType.CpiGuard]);

Instruction configureTransferFees = Token2022Program.InitializeTransferFeeConfig(
    mint,
    transferFeeAuthority.PublicKey,
    withdrawWithheldAuthority.PublicKey,
    basisPoints: 25,
    maximumFee: 1_000_000);

Instruction initializeMetadata = Token2022Program.InitializeTokenMetadata(
    metadata: mint,
    updateAuthority: metadataAuthority.PublicKey,
    mint: mint,
    mintAuthority: mintAuthority.PublicKey,
    name: "Example Token",
    symbol: "EX",
    uri: "https://example.invalid/token.json");

Instruction pointAtMetadata = Token2022Program.InitializeMetadataPointer(
    mint,
    authority: metadataAuthority.PublicKey,
    metadataAddress: mint);
```

Extension initializers for a new mint must be placed before the base `TokenProgram.InitializeMint`
instruction; `Reallocate` is for extending an existing token account and requires its payer and owner
signatures.

```csharp
using SolSharp.Programs;

Instruction initializeGroup = Token2022Program.InitializeTokenGroup(
    groupAccount,
    groupMint,
    mintAuthority.PublicKey,
    updateAuthority: groupAuthority.PublicKey,
    maximumSize: 10_000);

byte[] groupBytes = (await rpc.GetAccountInfoAsync(groupAccount))!.Data;
TokenGroupState group = TokenGroupState.Decode(groupBytes)
    ?? throw new InvalidOperationException("Malformed token-group state");
Console.WriteLine($"{group.Size}/{group.MaximumSize}");
```

Transfer-hook extra accounts may be literal keys or PDAs derived from instruction/account data. The
resolver follows the SPL TLV/seed contract in order, de-escalates duplicate privileges, and appends the
hook program plus validation PDA to a Token-2022 transfer:

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Programs;

async ValueTask<ReadOnlyMemory<byte>?> FetchAccountData(PublicKey address, CancellationToken ct)
    => (await rpc.GetAccountInfoAsync(address, cancellationToken: ct))?.Data;

var token2022 = PublicKey.Parse(SolanaProgramIds.Token2022Program);
var transfer = TokenProgram.TransferChecked(
    source, mint, destination, owner.PublicKey, amount, decimals, token2022);

Instruction transferWithHookAccounts = await TransferHookProgram.AddExtraAccountsForExecuteAsync(
    transfer,
    hookProgramId,
    source,
    mint,
    destination,
    owner.PublicKey,
    amount,
    FetchAccountData);
```

Confidential builders intentionally accept exact caller-generated ciphertext/proof PODs through
`ConfidentialProofLocation` and `ElGamalProofProgram`. SolSharp validates widths, instruction offsets,
accounts, and discriminators, but does not claim to generate or verify zero-knowledge proofs locally;
use a compatible audited cryptographic provider for those bytes.

For example, a public-key-validity proof can be verified immediately before creating its registry. The
relative offset is measured from the registry instruction, so `-1` refers to the preceding verifier:

```csharp
Instruction verifyRegistryKey = ElGamalProofProgram.VerifyProof(
    ElGamalProofInstruction.VerifyPubkeyValidity,
    publicKeyValidityProofData);
Instruction createRegistry = ElGamalRegistryProgram.CreateRegistry(
    owner.PublicKey,
    ConfidentialProofLocation.AtInstructionOffset(-1));

Instruction[] registrySetup = [verifyRegistryKey, createRegistry];

var registryAddress = ElGamalRegistryProgram.GetRegistryAddress(owner.PublicKey);
byte[] registryBytes = (await rpc.GetAccountInfoAsync(registryAddress))!.Data;
ElGamalRegistryState registry = ElGamalRegistryProgram.DecodeState(registryBytes)
    ?? throw new InvalidOperationException("Malformed ElGamal registry state");
```

Ordinary confidential transfers and withdrawals use the same proof-location model. In this example every
proof has already been verified into a context-state account; use signed relative offsets instead when the
proof instructions are composed into the same transaction:

```csharp
Instruction confidentialTransfer = Token2022Program.TransferConfidentialTokens(
    source,
    mint,
    destination,
    newSourceDecryptableAvailableBalance,
    auditorCiphertextLow,
    auditorCiphertextHigh,
    owner.PublicKey,
    ConfidentialProofLocation.AtContextState(equalityContext),
    ConfidentialProofLocation.AtContextState(validityContext),
    ConfidentialProofLocation.AtContextState(rangeContext));

Instruction confidentialWithdraw = Token2022Program.WithdrawConfidentialTokens(
    source,
    mint,
    amount,
    decimals,
    newSourceDecryptableAvailableBalance,
    owner.PublicKey,
    ConfidentialProofLocation.AtContextState(equalityContext),
    ConfidentialProofLocation.AtContextState(rangeContext));
```

Permissioned confidential burns keep the mint's permissioned-burn authority distinct from the token
account owner. Proofs may be referenced by signed relative instruction offsets or by pre-verified context
accounts; any proof-verification instructions still have to be composed into the transaction separately:

```csharp
using SolSharp.Core.Primitives;
using SolSharp.Programs;

static Instruction BuildPermissionedConfidentialBurn(
    PublicKey tokenAccount,
    PublicKey mint,
    PublicKey permissionedBurnAuthority,
    PublicKey owner,
    ReadOnlySpan<byte> newDecryptableAvailableBalance,
    ReadOnlySpan<byte> auditorCiphertextLow,
    ReadOnlySpan<byte> auditorCiphertextHigh,
    PublicKey equalityContext,
    PublicKey validityContext,
    PublicKey rangeContext)
    => Token2022Program.BurnPermissionedConfidentialTokens(
        tokenAccount,
        mint,
        permissionedBurnAuthority,
        newDecryptableAvailableBalance,
        auditorCiphertextLow,
        auditorCiphertextHigh,
        owner,
        ConfidentialProofLocation.AtContextState(equalityContext),
        ConfidentialProofLocation.AtContextState(validityContext),
        ConfidentialProofLocation.AtContextState(rangeContext));
```

## Attaching a memo

```csharp
var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .AddInstruction(MemoProgram.Memo("gm", payer.PublicKey))  // signer(s) optional
    .Build(payer);
```

## Native programs and account state

The native client layer follows the pinned System, Stake, Vote, loader, Compute Budget, ALT, Memo, and
signature-precompile contracts. Composite helpers return instruction arrays without hiding the signers
that must be supplied to `TransactionBuilder`:

```csharp
Instruction[] payouts = SystemProgram.TransferMany(
    payer.PublicKey,
    (recipient, lamports),
    (feeCollector, feeLamports));

Instruction initializePrefunded = SystemProgram.CreateAccountAllowPrefund(
    prefundedAccount.PublicKey,
    space: accountDataLength,
    owner: targetProgramId);

Instruction[] createSeededNonce = SystemProgram.CreateNonceAccountWithSeed(
    payer.PublicKey,
    nonceAccount,
    nonceBase.PublicKey,
    seed: "durable-nonce",
    authority: nonceAuthority,
    lamports: nonceRent);

Instruction recoverNestedAta = AssociatedTokenAccount.RecoverNested(
    payer.PublicKey,
    ownerMint,
    nestedMint,
    tokenProgram: token2022);
```

`TransferMany` and seeded-nonce helpers return multiple ordinary instructions; add all of them to the
builder and supply every signer identified above. `RecoverNested` derives the three canonical ATA
addresses and emits the current one-byte instruction tag.

```csharp
using SolSharp.Programs;
using SolSharp.Wallet;

using var stakeAccount = Keypair.Generate();
var authorities = new StakeAuthorized(payer.PublicKey, payer.PublicKey);
var noLockup = new StakeLockup(UnixTimestamp: 0, Epoch: 0, Custodian: default);

Instruction[] createAndDelegate = StakeProgram.CreateAccountAndDelegateStake(
    payer.PublicKey,
    stakeAccount.PublicKey,
    voteAccount,
    authorities,
    noLockup,
    stakeLamports);

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstructions(createAndDelegate)
    .Build(payer, stakeAccount);
```

Loader helpers likewise preserve every signer and wire step. This creates a Loader V4 account, writes one
ELF chunk, and constructs the deploy instruction; the caller still chooses funding, chunking, simulation,
and transaction boundaries:

```csharp
using var programAccount = Keypair.Generate();
Instruction[] createProgram = LoaderV4Program.CreateBuffer(
    payer.PublicKey,
    programAccount.PublicKey,
    programLamports,
    programAuthority.PublicKey,
    programLength: checked((uint)elfBytes.Length),
    recipient: payer.PublicKey);
Instruction writeProgram = LoaderV4Program.Write(
    programAccount.PublicKey,
    programAuthority.PublicKey,
    offset: 0,
    bytes: elfBytes);
Instruction deployProgram = LoaderV4Program.Deploy(
    programAccount.PublicKey,
    programAuthority.PublicKey);
```

`UpgradeableBpfLoaderProgram` exposes the corresponding buffer, deploy, upgrade, authority, extend, close,
and ProgramData-PDA operations for Loader V3. `FeatureGateProgram.ActivateWithLamports` and
`RevokePendingActivation` mirror the governance-facing feature interface; they do not grant authority to
activate arbitrary cluster features.

Account decoders reject wrong sizes, option tags, discriminators, alignment, and hostile collection
counts before allocation. For example:

```csharp
byte[] stakeBytes = (await rpc.GetAccountInfoAsync(stakeAccountAddress))!.Data;
StakeAccountState stake = StakeAccountState.Parse(stakeBytes);

byte[] loaderBytes = (await rpc.GetAccountInfoAsync(programDataAddress))!.Data;
UpgradeableBpfLoaderState loader = UpgradeableBpfLoaderState.Parse(loaderBytes);

byte[] voteBytes = (await rpc.GetAccountInfoAsync(voteAccount))!.Data;
VoteStateVersions voteState = VoteStateVersions.Parse(voteBytes);
Console.WriteLine($"{voteState.Version}: {voteState.Votes.Count} tower entries");
```

Current sysvar IDs live in `Sysvars`; their bounded account decoders live in
`SolSharp.Core.SysvarStates`. Fetch and validate the account owner before parsing its exact layout:

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Core.SysvarStates;

var clockAccount = await rpc.GetAccountInfoAsync(PublicKey.Parse(Sysvars.Clock));
if (clockAccount is null || clockAccount.Owner != PublicKey.Parse(Sysvars.Owner))
    throw new InvalidOperationException("Clock sysvar is missing or has the wrong owner.");

ClockSysvarState clock = ClockSysvarState.Parse(clockAccount.Data);
Console.WriteLine($"slot {clock.Slot}, epoch {clock.Epoch}");
```

Clock, Rent, EpochSchedule, EpochRewards, LastRestartSlot, SlotHashes, SlotHistory, and StakeHistory
are decoded without unbounded collection allocations. `SlotHistorySysvarState.Check(slot)` returns
`Future`, `TooOld`, `Found`, or `NotFound` against the runtime's fixed 1,048,576-slot window.

Precompile helpers can embed one verification payload or reference bytes in another instruction through
typed offset records. This builds the exact self-contained Ed25519 layout consumed by Agave:

```csharp
using SolSharp.Programs;
using SolSharp.Wallet;

using var signer = Keypair.Generate();
byte[] payload = "authorize session"u8.ToArray();
Signature signature = signer.SignSignature(payload);

Instruction verify = Ed25519Program.CreateInstruction(
    payload,
    signature.ToBytes(),
    signer.PublicKey.ToBytes());
```

Secp256k1 and Secp256r1 use the same account-free precompile model. The Rust-compatible Secp256k1
convenience helper writes transaction instruction index 0 into its offset record, so add the returned
instruction first in the transaction. For any other position, use `CreateOffsetsInstruction` with explicit
instruction indexes. Ed25519 and Secp256r1 self-contained helpers use the current-instruction sentinel and
do not have this position constraint. Supply signatures produced by the appropriate external curve
implementation; SolSharp constructs the exact verifier layouts:

```csharp
// This helper must be transaction instruction 0.
Instruction verifyEthereumSignature = Secp256k1Program.CreateInstruction(
    payload,
    compactSecp256k1Signature,
    recoveryId,
    ethereumAddress);
Instruction verifyPasskeySignature = Secp256r1Program.CreateInstruction(
    payload,
    compactLowSSecp256r1Signature,
    compressedSecp256r1PublicKey);
```

`InstructionsSysvar.Serialize`, `ReadInstruction`, and `ReadInstructionRelative` expose the native
instruction-introspection account layout off chain, which is useful for validating those cross-instruction
offsets before submitting a transaction.

## Versioned (v0) transactions and address lookup tables

A v0 transaction can load extra accounts from an on-chain Address Lookup Table (ALT) instead of listing
them all in the message. Fetch the table, wrap it, hand it to the builder, and call `BuildV0`.

```csharp
using SolSharp.Programs;

var tableKey = PublicKey.Parse("…");

// Fetch + decode the table (SolSharp.Rpc model), then wrap it for the builder.
var fetched = await rpc.GetAddressLookupTableAsync(tableKey)
    ?? throw new InvalidOperationException("lookup table not found");
if (fetched.IsUsable is not true)
    throw new InvalidOperationException("lookup table usability cannot be established without SlotHashes");
var table = new AddressLookupTableAccount(tableKey, fetched.Addresses);

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .SetAddressLookupTables(table)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .BuildV0(payer);

await rpc.SendTransactionAsync(tx.Serialize());
```

Accounts that appear in the table (and are not signers or program IDs) are drained out of the static keys
and referenced through the table, shrinking the transaction. Building and managing the table itself is done
with `AddressLookupTableProgram` (`CreateLookupTable`, `ExtendLookupTable`, `FreezeLookupTable` — permanently
locks the table immutable, `DeactivateLookupTable`, `CloseLookupTable`).

For canonical table creation, only the payer signs; the future table authority is a read-only non-signer,
matching the currently activated Solana runtime behavior.

`IsActive` means only that deactivation has not begun. A deactivating table remains usable during its
SlotHashes cooldown, so transaction code should inspect nullable `IsUsable`: `true` is known usable and
`null` means the RPC response lacks enough SlotHashes history to decide safely. `Addresses` is the
context-visible prefix and deliberately hides entries appended in the response slot; `StoredAddresses`
retains the full serialized list for inspection.

When the application has fetched the SlotHashes sysvar itself, the standalone Programs decoder exposes the
exact Rust SDK decision instead of the RPC model's conservative estimate: parse `AddressLookupTableState`,
then call `GetStatus`, `IsActive`, `GetActiveAddresses`, or `Lookup` with the current slot and a decoded
`SlotHashesSysvarState`. Same-slot extensions remain hidden and a table stays active throughout cooldown.

## SIMD-0385 V1 transactions

V1 is the current feature-gated transaction format in the pinned Solana SDK. It stores all account addresses
inline (there are no address lookup tables), carries compute and fee settings in the message itself, begins
with `0x81`, and places its fixed number of signatures **after** the message. Build it explicitly with
`SetV1Config` and `BuildV1`:

```csharp
using SolSharp.Programs;

var v1 = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .SetV1Config(new TransactionConfigV1
    {
        PriorityFee = 5_000,                    // total lamports, not micro-lamports per CU
        ComputeUnitLimit = 200_000,
        LoadedAccountsDataSizeLimit = 64 * 1024,
        HeapSize = 32 * 1024
    })
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, lamports))
    .BuildV1(payer);

byte[] wire = v1.Serialize();
Console.WriteLine(v1.Version); // V1
```

Do not submit an empty `TransactionConfigV1` by accident: its missing compute-unit and loaded-account-data
limits mean zero, so it is normally unusable at runtime; only the omitted heap size has a nonzero default
(32 KiB). Current V1 limits are 64 accounts, 64 instructions, 12 signatures, and a 4096-byte RPC/runtime
admission limit. The codec deliberately round-trips larger wire payloads like the pinned Rust SDK; the node
enforces admission.

V1 is controlled by the cluster feature `enable_tx_v1`
(`SolanaFeatureIds.EnableTransactionV1`). Check activation on the target cluster before sending; library
support does not imply that a particular validator or RPC endpoint has enabled the feature:

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Programs;

var featureKey = PublicKey.Parse(SolanaFeatureIds.EnableTransactionV1);
var featureOwner = PublicKey.Parse(SolanaProgramIds.FeatureProgram);
var featureAccount = await rpc.GetAccountInfoAsync(featureKey);

bool v1Enabled = featureAccount is not null
    && featureAccount.Owner == featureOwner
    && !featureAccount.Executable
    && FeatureAccountState.TryParse(featureAccount.Data, out var feature)
    && feature!.IsActive;
```

## Decoding a transaction

Parse a serialized transaction (from `getTransaction`, a log, or a wallet) back into a `Transaction`.

```csharp
using SolSharp.Programs;

byte[] raw = Convert.FromBase64String(base64Tx);
var tx = Transaction.Deserialize(raw);

Console.WriteLine(tx.Version); // Legacy, V0, or V1
Console.WriteLine($"required signers: {tx.Message.RequiredSignatures}");
foreach (var key in tx.Message.AccountKeys)
    Console.WriteLine(key);
```

Offline and multisig workflows use the required signer slots exposed by `RequiredSignerKeys` and `Signatures`.
`PartialSign` fills only matching slots and retains existing signatures; `AddSignature` verifies an externally
produced typed `Signature` against the exact message before inserting it. `SignAll` applies signers and fails if
any required slot remains absent. The older `Sign` name remains a compatibility alias for `PartialSign`.

```csharp
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

byte[] signable = tx.GetMessageBytes();       // send these exact bytes to an external signer
Signature external = Signature.Parse(externalSignatureBase58);

tx.PartialSign(payer)
  .AddSignature(cosignerPublicKey, external); // cryptographically verified before insertion

if (!tx.IsFullySigned || !tx.VerifySignatures())
    throw new InvalidOperationException("transaction signatures are incomplete or invalid");

Hash messageHash = tx.VerifyAndHashMessage();
string resubmittable = tx.ToBase64();
```

`VerifySignaturesWithResults()` returns one boolean per required signer, which is useful for showing exactly
which participant is still missing or invalid. A deserialized transaction retains the exact message bytes its
existing signatures cover, so later mutation of the object graph cannot silently retarget those slots.

### Analyzing a historical transaction

`getTransaction` returns the decoded bytes plus rich metadata. Parse the bytes, **decompile** the instructions
(resolving each account index to a public key and signer/writable flags), read the token-balance deltas, and
decode any failure into a typed error:

```csharp
var fetched = await rpc.GetTransactionAsync(signature);
if (fetched is not null)
{
    var parsed = Transaction.Deserialize(fetched.Transaction);

    // Resolve instructions to program ids + account keys. A v0 transaction loads accounts from lookup tables:
    var instructions = parsed.Message is MessageV0 v0
        ? v0.DecompileInstructions(await FetchTablesAsync(rpc, v0))
        : parsed.Message.DecompileInstructions();

    foreach (var ix in instructions)
        Console.WriteLine($"{ix.ProgramId} over {ix.Accounts.Count} accounts");

    foreach (var post in fetched.Meta?.PostTokenBalances ?? [])
        Console.WriteLine($"{post.Mint}: {post.UiTokenAmount.UiAmountString}");

    var wireVersion = fetched.Version?.IsLegacy is true
        ? "legacy"
        : fetched.Version?.Number?.ToString() ?? "omitted";
    Console.WriteLine($"version={wireVersion} index={fetched.TransactionIndex} " +
                      $"compute={fetched.Meta?.ComputeUnitsConsumed} cost={fetched.Meta?.CostUnits}");
    if (fetched.Meta?.ReturnData is { } returned)
        Console.WriteLine($"{returned.ProgramId} returned {returned.Data.Length} bytes");

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

The compatibility-preserving default raw read advertises legacy/v0. To fetch V1, opt into numeric version 1;
the returned bytes can be parsed locally:

```csharp
var v1 = await rpc.GetTransactionWithMaxVersionAsync(
    signature,
    maxSupportedTransactionVersion: 1,
    commitment: Commitment.Confirmed)
    ?? throw new InvalidOperationException("transaction not found");
var decodedV1 = Transaction.Deserialize(v1.Transaction);
```

The existing method names keep their v0 maximum for source and behavior compatibility. Use the explicitly named
`GetParsedTransactionWithMaxVersionAsync`, `GetParsedBlockWithMaxVersionAsync`,
`SubscribeBlocksWithMaxVersionAsync`, and `SubscribeParsedBlocksWithMaxVersionAsync` methods when opting into
V1. A parsed V1 message exposes its nullable settings through `tx.Message.TransactionConfig`.

### Walking an address's history, or a whole block

To find the transactions in the first place, page through an address's signatures (newest first) or
fetch a block:

```csharp
// Page backwards through an address's history:
var page = await rpc.GetSignaturesForAddressAsync(account,
    new GetSignaturesForAddressOptions { Limit = 100 });
foreach (var entry in page)
    Console.WriteLine($"{entry.Signature} slot={entry.Slot} failed={entry.Err is not null}");

if (page.Count > 0)
{
    var older = await rpc.GetSignaturesForAddressAsync(account,
        new GetSignaturesForAddressOptions { Before = page[^1].Signature });
    Console.WriteLine($"older page: {older.Count} entries");
}

// A block's transaction signatures (feed each to GetTransactionAsync as needed):
var block = await rpc.GetBlockAsync(slot);          // null when the slot was skipped
if (block is not null)
{
    foreach (var blockSignature in block.Signatures)
        Console.WriteLine(blockSignature);
}

// Or every transaction in the block already decoded by the node (indexer-style):
var parsedBlock = await rpc.GetParsedBlockAsync(slot);
if (parsedBlock is not null)
{
    foreach (var entry in parsedBlock.Transactions)
        Console.WriteLine($"fee: {entry.Meta?.Fee}");
}

// Schema-changing upstream choices stay lossless as JSON instead of being projected into
// one misleading model. Here only signatures and block rewards are requested:
var configuredBlock = await rpc.GetBlockWithOptionsAsync(
    slot,
    new GetBlockOptions
    {
        Encoding = RpcTransactionEncoding.Base64,
        TransactionDetails = RpcTransactionDetails.Signatures,
        Rewards = true,
        Commitment = Commitment.Finalized,
        MaxSupportedTransactionVersion = 1
    });

// The same exact-encoding path is available for one transaction:
var configuredTransaction = await rpc.GetTransactionWithOptionsAsync(
    signature,
    new GetTransactionOptions
    {
        Encoding = RpcTransactionEncoding.Json,
        Commitment = Commitment.Confirmed,
        MaxSupportedTransactionVersion = 1
    });
```

## Reading parsed transactions

When you'd rather not Borsh-decode instructions yourself, ask the node to do it: the `jsonParsed` encoding
returns recognized instructions, token balances and logs already decoded. SolSharp exposes this as a separate
read path that sits alongside the raw one. The upstream response is a union: a recognized instruction carries
its typed `Parsed` action, while an unrecognized instruction carries raw `ProgramId` / `Accounts` / `Data`.
SolSharp preserves whichever branch the node returned without inventing fields absent from that branch.

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
var parsedTx = await rpc.GetParsedTransactionAsync(signature)
    ?? throw new InvalidOperationException("transaction not found");
var transfer = parsedTx.Message.Instructions.First(ix => ix.Parsed?.Type == "transfer");
ulong lamports = transfer.Parsed!.Info.GetProperty("lamports").GetUInt64();
```

`GetParsedBlockAsync(slot)` returns a whole block of parsed transactions and enriches each entry with its
`Slot`, `BlockTime`, and ledger-order `TransactionIndex`. `SubscribeParsedBlocksAsync` streams the node's
parsed block payload together with `ParsedBlockNotification.Slot`; transaction-level `Slot`, `BlockTime`,
and `TransactionIndex` remain `null` in that streaming shape because PubSub does not place them on each
transaction. As with the raw path, `GetParsedTransactionAsync` returns `null` when the signature isn't found
and `GetParsedBlockAsync` returns `null` for a skipped slot.

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

foreach (var node in nodes)
    Console.WriteLine($"{node.ClientId} {node.TpuQuic} {node.Pubsub}");

// Staking rewards paid to a set of addresses for a given epoch (null per address when there were none):
var rewards = await rpc.GetInflationRewardAsync([voteAccount], epoch: 600);

// Full variants expose every effective pinned config field without changing convenient defaults:
var context = new RpcContextOptions
{
    Commitment = Commitment.Confirmed,
    MinContextSlot = lastObservedSlot
};
var latest = await rpc.GetLatestBlockhashWithOptionsAsync(context);
var oneValidator = await rpc.GetVoteAccountsWithOptionsAsync(
    new GetVoteAccountsOptions
    {
        VotePublicKey = voteAccount,
        KeepUnstakedDelinquents = true,
        DelinquentSlotDistance = 128
    });
var oneLeader = await rpc.GetLeaderScheduleWithOptionsAsync(
    new GetLeaderScheduleOptions { Identity = validator });
var fullSupply = await rpc.GetSupplyWithOptionsAsync(
    new GetSupplyOptions { ExcludeNonCirculatingAccountsList = false });
var sortedLargest = await rpc.GetLargestAccountsWithOptionsAsync(
    new GetLargestAccountsOptions
    {
        Filter = LargestAccountsFilter.Circulating,
        SortResults = true
    });
var contextualRewards = await rpc.GetInflationRewardWithOptionsAsync(
    [voteAccount],
    new GetInflationRewardOptions { Epoch = 600, MinContextSlot = lastObservedSlot });
```

Epoch structure, inflation, and network identity:

```csharp
var epochSchedule = await rpc.GetEpochScheduleAsync();      // slots per epoch, warmup, offsets
var governor = await rpc.GetInflationGovernorAsync();       // inflation parameters
var rate = await rpc.GetInflationRateAsync();               // current total/validator/foundation split
var genesis = await rpc.GetGenesisHashAsync();              // identifies the network (mainnet/devnet/...)
var agGenesis = await rpc.GetAgGenesisCertificateAsync();   // null until Alpenglow consensus is active
var identity = await rpc.GetIdentityAsync();                // the queried node's identity key
var leader = await rpc.GetSlotLeaderAsync();                // current slot leader
var minStake = await rpc.GetStakeMinimumDelegationAsync();  // minimum stake delegation, lamports
```

Block timing, production, and history bounds:

```csharp
var time = await rpc.GetBlockTimeAsync(slot);               // Unix seconds, null when unavailable
var limited = await rpc.GetBlocksWithLimitAsync(slot, 10);  // up to N confirmed slots from a start
var commitment = await rpc.GetBlockCommitmentAsync(slot);   // stake voted per confirmation depth

// Leader slots vs. blocks actually produced, per validator (current epoch by default):
var production = await rpc.GetBlockProductionAsync(identity: validator, firstSlot: 100, lastSlot: 200);
foreach (var (validatorIdentity, counts) in production.ByIdentity)
    Console.WriteLine($"{validatorIdentity}: {counts.BlocksProduced}/{counts.LeaderSlots}");

var first = await rpc.GetFirstAvailableBlockAsync();        // oldest block the node still has
var minLedger = await rpc.GetMinimumLedgerSlotAsync();      // lowest slot in the node's ledger
var snapshot = await rpc.GetHighestSnapshotSlotAsync();     // full + incremental snapshot slots
```

Node throughput and the largest wallets:

```csharp
// TPS material: transactions and slots per sampled window, newest first (max 720 samples):
var samples = await rpc.GetRecentPerformanceSamplesAsync(limit: 60);
var tps = samples.Average(s => (double)s.NumTransactions / s.SamplePeriodSecs);

// The 20 largest accounts by balance, optionally one side of the circulating-supply split:
var largest = await rpc.GetLargestAccountsAsync(filter: LargestAccountsFilter.Circulating);

var retransmit = await rpc.GetMaxRetransmitSlotAsync();     // highest slot from retransmitted shreds
var inserted = await rpc.GetMaxShredInsertSlotAsync();      // highest slot inserted into the ledger
```

And the basics — node health and version, chain progress, SOL supply, upcoming leaders, and
blockhash liveness:

```csharp
var healthy = await rpc.GetHealthAsync();                   // false when the node is behind
var version = await rpc.GetVersionAsync();                  // solana-core version + feature set
var height = await rpc.GetBlockHeightAsync();               // blocks below the current slot
var txCount = await rpc.GetTransactionCountAsync();         // transactions processed by the cluster
var solSupply = await rpc.GetSupplyAsync();                 // total / circulating / non-circulating
var leaders = await rpc.GetSlotLeadersAsync(slot, 10);      // the next 10 slot leaders from a slot
var alive = await rpc.IsBlockhashValidAsync(blockhash);     // can this blockhash still anchor a tx?
```

## WebSocket subscriptions

All subscriptions share one connection and survive dropped connections (auto-reconnect + resubscribe).
Slots, roots, slot updates, and votes arrive as `IAsyncEnumerable`; account, program, logs, signature,
and block subscriptions return a `ChannelReader`.

```csharp
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Streaming;

await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://api.mainnet-beta.solana.com"));
var tokenProgram = PublicKey.Parse(SolanaProgramIds.TokenProgram);

// Slots:
await foreach (var slot in ws.SubscribeSlotsAsync())
    Console.WriteLine(slot.Slot);

// Logs mentioning a program (ChannelReader):
var logs = await ws.SubscribeLogsAsync(tokenProgram);
await foreach (var note in logs.ReadAllAsync())
    Console.WriteLine(note.Value!.Signature);

// The exact upstream filter union also supports all non-vote logs, or all logs including votes:
var allLogs = await ws.SubscribeLogsWithFilterAsync(LogsSubscriptionFilter.AllWithVotes);

// Account changes:
var accounts = await ws.SubscribeAccountAsync(tokenProgram);
await foreach (var note in accounts.ReadAllAsync())
    Console.WriteLine(note.Value!.Lamports);

// Preserve the node's exact account-data branch while selecting an effective PubSub encoding:
var exactAccounts = await ws.SubscribeAccountWithOptionsAsync(
    tokenProgram,
    new AccountSubscriptionOptions
    {
        Encoding = RpcAccountEncoding.JsonParsed,
        Commitment = Commitment.Confirmed
    });
await foreach (var note in exactAccounts.ReadAllAsync())
{
    var data = note.Value!.Data;
    if (data is RpcAccountData.Parsed parsed)
        Console.WriteLine($"{parsed.Program}: {parsed.Value}");
    else if (data is RpcAccountData.Encoded fallback)
        Console.WriteLine($"fallback: {fallback.Encoding}");
}

// programSubscribe applies the same encoding/commitment pair plus the full account-filter union:
var ownerBytes = PublicKey.Parse(SolanaProgramIds.SystemProgram).ToBytes();
var programAccounts = await ws.SubscribeProgramWithOptionsAsync(
    tokenProgram,
    new ProgramSubscriptionOptions
    {
        Encoding = RpcAccountEncoding.Base64Zstd,
        Commitment = Commitment.Confirmed,
        Filters =
        [
            AccountFilter.DataSize(165),
            AccountFilter.DataSizeUnsigned(165UL), // accepts the full upstream u64 range
            AccountFilter.MemoryCompareRaw(32, ownerBytes),
            AccountFilter.TokenAccountState()
        ]
    });

// Ask for the optional early "receivedSignature" event, then the final processed result:
var signatureEvents = await ws.SubscribeSignatureWithOptionsAsync(
    transactionSignature,
    new SignatureSubscriptionOptions { EnableReceivedNotification = true });
await foreach (var note in signatureEvents.ReadAllAsync())
    Console.WriteLine(note.Value!.Kind);

// A schema-changing block configuration is deliberately returned as raw JSON:
var rawBlocks = await ws.SubscribeBlocksWithOptionsAsync(
    BlockSubscriptionFilter.Mentions(someProgram),
    new BlockSubscriptionOptions
    {
        Encoding = RpcTransactionEncoding.Base64,
        TransactionDetails = RpcTransactionDetails.Signatures,
        ShowRewards = true,
        MaxSupportedTransactionVersion = 1
    });
```

Also available: `SubscribeRootsAsync` (rooted slots, like `SubscribeSlotsAsync`), `SubscribeProgramAsync`
(with base58/base64/raw memcmp, unsigned data-size, and token-account-state filters),
`SubscribeSignatureAsync`, `SubscribeBlocksAsync`, and the `jsonParsed`
streams `SubscribeParsedBlocksAsync` / `SubscribeParsedAccountAsync` / `SubscribeParsedProgramAsync`.
Cancel any channel subscription by
cancelling the `CancellationToken` you pass in. A returned `ChannelReader` supports multiple concurrent
consumers; each notification is delivered to one reader.

`RpcAccountEncoding.Binary` yields `RpcAccountData.LegacyBinary` (the bare legacy base58 string).
`Base58`, `Base64`, and `Base64Zstd` yield a tagged `RpcAccountData.Encoded`; `JsonParsed` yields
`RpcAccountData.Parsed` for a program the node recognizes and a base64 `Encoded` fallback otherwise.
Pinned Agave applies only encoding and commitment to account notifications, while program notifications also
apply filters. Shared config fields such as account/program `dataSlice` and `minContextSlot`, plus program
`withContext` and `sortResults`, are accepted but ignored by those PubSub encoders, so
`AccountSubscriptionOptions` and `ProgramSubscriptionOptions` do not pretend that they work; use the
corresponding HTTP options when those fields are required.

Two more streams cover the slot lifecycle in depth — both are marked *unstable* by Solana, so their
wire shape can change between node versions:

```csharp
// Every stage a slot moves through: firstShredReceived, createdBank, frozen (with
// transaction stats), optimisticConfirmation, root, dead. Richer than SubscribeSlotsAsync:
await foreach (var update in ws.SubscribeSlotsUpdatesAsync())
    Console.WriteLine($"{update.Slot} {update.Type} fails={update.Stats?.NumFailedTransactions}");

// Votes as they arrive in gossip, before they land in a block. The node must run with
// --rpc-pubsub-enable-vote-subscription, otherwise the subscribe is rejected:
await foreach (var vote in ws.SubscribeVotesAsync())
    Console.WriteLine($"{vote.VotePubkey} voted on {vote.Slots[^1]}");
```

The reconnect policy is tunable through `SolanaWsClientOptions`: `AutoReconnect` (on by default), the
`ReconnectInitialDelay` → `ReconnectMaxDelay` exponential backoff, and `MaxReconnectAttempts` (`0` retries
forever). When reconnect attempts are exhausted — or auto-reconnect is off — every subscription completes
with the connection error. `SubscriptionAckTimeout` (30 seconds by default) bounds both initial subscribe
and reconnect replay acknowledgement waits, so one unresponsive request cannot stall every subscription
behind it. `MaxPendingSubscriptionRequests` (1,024 by default) caps the combined number of live ACK waits
and compact late-ACK cleanup records. After a timeout or cancellation, the subscription, sink, and request
parameters are released while a generation-scoped cleanup record remains so a late successful ACK can still
be unsubscribed; once the cap is reached, further subscriptions fail before they are sent until an ACK or
connection drop frees space.

`ReceiveTimeout` (off by default) treats a connection with no complete message for the given interval as
dropped, so auto-reconnect can replace a silently half-open socket. Only data messages surfaced to the
receive loop reset the timer; protocol ping/pong frames do not. Enable it only when subscribed traffic is guaranteed
to be frequent (slot subscriptions, busy programs): on a legitimately quiet subscription, such as an
account that rarely changes, it would force a reconnect cycle — and a notification gap — every interval.

Incoming data is bounded in both directions. `MaxMessageSizeBytes` (64 MiB by default) rejects an oversized
WebSocket message; the connection then closes and, with auto-reconnect on, is re-established — raise the
limit if you stream heavy parsed blocks. `SubscriptionBufferCapacity` (1,024 by default) limits unread
notifications per subscription: a consumer that falls behind is faulted and unsubscribed rather than
consuming memory without bound, so raise it for bursty feeds like `programSubscribe` on a busy program.
The item limit is reinforced by encoded-size budgets: `MaxBufferedNotificationBytesPerSubscription`
(64 MiB by default) and `MaxBufferedNotificationBytesTotal` (256 MiB by default). Each unread notification
is charged by the UTF-8 size of its full JSON-RPC WebSocket message. A subscription that would exceed either
byte budget is faulted and unsubscribed. Reading an item releases its charge; writer completion and reconnect
do not release unread items because completed channel readers can still drain their buffered data. Stopping an
`IAsyncEnumerable` early discards its private unread backlog and releases that budget immediately.
Other subscriptions and the shared connection continue running. A subscribe the node rejects throws
`InvalidOperationException` carrying the node error code and message; a notification that fails to decode
also faults only its own subscription. Disposing the client completes every channel and stream.

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

Both confirmation paths accept any non-negative timeout (or `Timeout.InfiniteTimeSpan`); long WebSocket
timeouts are chunked internally instead of hitting the platform timer limit.

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
`AdvanceNonceAccount` instruction, so each submission consumes the nonce exactly once. The two anchoring
modes are mutually exclusive: calling `SetRecentBlockhash` afterward switches the builder back to blockhash
anchoring and drops the pending `AdvanceNonceAccount`, just as `SetDurableNonce` replaces a previously set
blockhash. A nonce-advance-only transaction is valid too; no additional instruction is required.

`CreateNonceAccount` above is a convenience pair — `CreateAccount` + `InitializeNonceAccount`, also
available separately. `CreateNonceAccountWithSeed` returns the corresponding seeded create+initialize
pair when the nonce address was derived with `ProgramDerivedAddress.CreateWithSeed`. The rest of the nonce
lifecycle is one instruction each:
`SystemProgram.WithdrawNonceAccount(nonceAccount, authority, recipient, lamports)` moves lamports out of
the account, and `SystemProgram.AuthorizeNonceAccount(nonceAccount, authority, newAuthority)` hands it to
a new authority. To migrate a legacy nonce-state account, add
`SystemProgram.UpgradeNonceAccount(nonceAccount)` to a transaction; the nonce account is writable but no
authority signature is required by that instruction.

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

// The create_program_address counterpart: derive from explicit seeds (bump included, no search).
// Returns false when the result lands on the curve:
if (ProgramDerivedAddress.TryCreateProgramAddress([seed, bumpSeed], programId, out var address))
    Console.WriteLine(address);

// System create_with_seed derivation is SHA-256(base || UTF-8 seed || owner).
// Unlike a PDA, its result is allowed to be on the Ed25519 curve.
var seededAddress = ProgramDerivedAddress.CreateWithSeed(baseAddress, "vault", ownerProgram);
```

A derivation accepts at most `MaxSeeds` (16) seeds — the bump counts toward the limit — of up to
`MaxSeedLength` (32) bytes each, matching the runtime's rules. `CreateWithSeed` takes one UTF-8 seed of
at most 32 encoded bytes and enforces the System Program's reserved-owner suffix rule.

## Batching RPC calls

`CreateBatch` queues several calls and submits them as one JSON-RPC batch — one HTTP round-trip instead of
N. Await the queued tasks only **after** starting `ExecuteAsync`; a per-call node error faults only that
call's task.

```csharp
var batch = rpc.CreateBatch();
var balance = batch.GetBalanceAsync(wallet.PublicKey);
var blockhash = batch.GetLatestBlockhashAsync();
var slot = batch.GetSlotAsync();

await batch.ExecuteAsync();   // one round-trip

Console.WriteLine($"{await balance} lamports at slot {await slot}");
```

Sends batch too — submit several signed transactions at once with `batch.SendTransactionAsync(bytes)`.
Note that some RPC providers disable or cap JSON-RPC batching; a non-batch reply surfaces as an
`RpcException`.

## Allocation-free serialization

`Serialize()` already allocates exactly one right-sized array per call. For hot paths that want zero
allocations — or to write straight into a reusable buffer — pair `GetSerializedLength()` with
`TrySerialize(Span<byte>, out int)`:

```csharp
using SolSharp.Programs;

// Allocate once and reuse. Legacy/v0 admission is capped at 1232 bytes; SIMD-0385 V1 admits up to 4096.
var reusableBuffer = new byte[MessageV1.MaxTransactionSize];
Span<byte> buffer = reusableBuffer;

if (!tx.TrySerialize(buffer, out var written))
    throw new InvalidOperationException("buffer too small");

ReadOnlySpan<byte> wire = buffer[..written]; // hand to your transport without an intermediate array
```

`TrySerialize` writes nothing and returns `false` when the span is smaller than `GetSerializedLength()`
bytes. (`SolanaRpcClient.SendTransactionAsync` takes a `byte[]`, so with the typed client plain
`Serialize()` is the natural fit; the span path pays off with custom transports and pooled buffers.)

The same pattern exists one level down: `Message`, `MessageV0`, and `MessageV1` (via `ITransactionMessage`) expose
`GetSerializedLength()` and a span-writing `Serialize(Span<byte>)` overload for working with raw message
bytes before signing.

## Rate limits, custom endpoints, and headers

`AddSolanaRpc` takes an options delegate and an optional resilience delegate; the returned builder is a
standard `IHttpClientBuilder`, so you can add headers or swap the handler.

```csharp
using Microsoft.Extensions.DependencyInjection;
using SolSharp.Rpc;

services.AddSolanaRpc(
        options =>
        {
            options.Endpoint = "https://your-node.example/<token>";
            // Default: 128 MiB. Raise only when a provider returns larger legitimate block/account payloads.
            options.MaximumResponseContentLength = 256 * 1024 * 1024;
        },
        resilience =>
        {
            resilience.Retry.MaxRetryAttempts = 5;          // back off harder on a busy provider
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        })
    .ConfigureHttpClient(http =>
        http.DefaultRequestHeaders.Add("x-api-key", apiKey)); // auth header for the provider
```

Transient reads and replay-safe signed transaction submissions use that retry policy. `RequestAirdropAsync`
is explicitly excluded: if a node executes an airdrop but its response is lost, automatically repeating the
request would create a second airdrop.

The response limit applies to both single and batch calls and is enforced while the HTTP body is streamed,
before the complete JSON document is buffered.

## Error handling

- **`RpcException`** — the node returned a JSON-RPC error; `Code` and `Message` carry the summary, while
  `ErrorData` preserves optional structured diagnostics such as preflight logs and units consumed.
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
    if (ex.ErrorData is { } data)
        Console.WriteLine(data.GetRawText());
}
```

## Publishing with Native AOT

SolSharp is Native AOT compatible out of the box — all JSON is source-generated (no reflection),
and every assembly is trimmable and builds clean under the trim/AOT analyzers. No extra configuration
is needed for managed functionality; enable AOT in your project:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r linux-x64
```

The result is a self-contained native binary with instant startup and no JIT — well suited to
bots and short-lived CLI tools. A complete working example lives in
[`samples/SolSharp.AotSmoke`](../samples/SolSharp.AotSmoke), which CI publishes natively and runs
on every push and pull request targeting `main`.

Two things to know when your own code meets AOT:

- **Your own JSON models need their own source generation.** `SolanaJsonSerializer.Options` covers
  the SolSharp wire primitives only. If you serialize your own types, declare a
  `JsonSerializerContext` for them; the SolSharp wire primitives (`PublicKey`, `Hash`, `Commitment`) keep their
  wire format under any options because their mappings live in `[JsonConverter]` attributes — and
  the public `CoreJsonContext` can be chained into your resolver if you register models that
  contain them.
- **Everything else is just C#.** Transaction building, signing, Borsh decoding, and the RPC/WS
  clients use no reflection, so no `rd.xml`, no trimmer hints, and no `DynamicDependency`
  annotations are required. BLS12-381 operations are the one native-backend exception: the dependency
  ships AOT-compatible assets for `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, and `win-x64`.
