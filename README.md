<p align="center">
  <img src="https://raw.githubusercontent.com/jecacs/SolSharp/main/assets/logo.png" alt="SolSharp" width="180" />
</p>

# SolSharp

[![NuGet](https://img.shields.io/nuget/v/SolSharp.svg?logo=nuget)](https://www.nuget.org/packages/SolSharp)
[![Downloads](https://img.shields.io/nuget/dt/SolSharp.svg?logo=nuget)](https://www.nuget.org/packages/SolSharp)
[![build](https://github.com/jecacs/SolSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/jecacs/SolSharp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lean, modern .NET SDK for Solana — RPC, WebSocket streaming, and wire-level
transaction signing and building.

SolSharp is built for low latency and a small dependency footprint. It is a focused,
hackable alternative to the heavier general-purpose SDKs: you get direct control over the
wire format and the signing path, without dragging in a large dependency graph. If you are
writing bots, indexers, or backend services that talk to Solana from .NET and care about
speed and control, this is aimed at you.

> **Status: 0.4.1 — stable release.** SolSharp ships as a single NuGet package — `SolSharp` —
> bundling the Core (primitives + encodings), Wallet (Ed25519 keys, signing, verification), Rpc (HTTP
> reads + send/simulate + WebSocket streaming + DI), and Programs (instructions + transaction building +
> signing) assemblies. Versioning follows semver; while on 0.x, minor releases may still carry breaking
> changes.

📖 **New here? Read the [usage guide](docs/USAGE.md)** — a task-oriented cookbook covering keys, reads,
SPL token state, building/signing/sending transactions, v0 + address lookup tables, decoding transactions,
WebSocket subscriptions, confirmation, and more.

## Motivation

When this was started, the .NET options for Solana were either unmaintained and stale or
heavy and not built for performance — there was no modern, fast, actively-developed client.
SolSharp is a from-scratch answer to that: current C#, allocation-conscious, and tuned for
latency-sensitive workloads.

## Why

- **Lean.** No kitchen-sink dependency graph. `Core` depends on a single package (base58).
- **Wire-level control.** Hand-rolled, spec-accurate transaction and message encoding — the part
  most SDKs hide — with Ed25519 signing on a vetted crypto library, all tested against known vectors.
- **Latency-minded.** Value types, allocation-free hot paths, span-based APIs.
- **Modern .NET.** C# latest, nullable reference types, code style enforced on build.

## Package

SolSharp ships as a **single NuGet package** — `SolSharp` — so one `dotnet add package SolSharp` pulls in
everything. Internally it stays four layered assemblies, bundled into that one package (namespaces are
unchanged: `SolSharp.Core.*`, `SolSharp.Rpc`, `SolSharp.Wallet`, `SolSharp.Programs`):

Install from [NuGet](https://www.nuget.org/packages/SolSharp):

```bash
dotnet add package SolSharp
```

```xml
<PackageReference Include="SolSharp" Version="0.4.1" />
```

| Assembly           | Purpose                                              | Status |
| ------------------ | --------------------------------------------------- | ------ |
| `SolSharp.Core`    | Primitives, encoding, JSON, program/sysvar constants | Usable |
| `SolSharp.Wallet`  | Ed25519 keys, key parsing, signing and verification | Usable |
| `SolSharp.Rpc`     | HTTP JSON-RPC reads + WebSocket streaming + DI       | Usable |
| `SolSharp.Programs`| Instructions (System/Token/ATA/Memo/Compute Budget/ALT) + transaction building | Usable |

Keeping the split in the source means the layering stays compiler-enforced — dependencies point downward
only: `Rpc` and `Wallet` build on `Core`, and `Programs` builds on `Core` and `Wallet`. `Core` depends on
nothing else in the solution and pulls no network or crypto package.

See the [changelog](CHANGELOG.md) for what changed in each release.

## What's here today

`SolSharp.Core`:

- `PublicKey` — a 32-byte value type with value equality, base58 parsing, and JSON support.
- `Base58`, `ShortVec` (compact-u16), and `BorshReader` / `BorshWriter` — the encodings Solana uses on the
  wire, plus a bounds-checked reader and writer for Anchor / Borsh account data and instruction arguments.
- `Commitment` — an RPC enum that serializes to its exact wire string.
- `SolanaProgramIds`, `Sysvars`, `Mints` — well-known on-chain addresses, guarded by a test
  that every constant decodes to a valid 32-byte key.
- `SolanaUnits` — SOL ↔ lamports conversion.

```csharp
using SolSharp.Core.Primitives;

var mint = PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");
byte[] raw = mint.ToBytes();              // 32 bytes, allocation-free storage
bool ok = PublicKey.TryParse(input, out var key);
```

`SolSharp.Rpc`:

- HTTP JSON-RPC reads — accounts (`getAccountInfo`, `getMultipleAccounts`, `getProgramAccounts` with
  memcmp / data-size filters and data slices, `getTokenAccountsByOwner`, `getTokenLargestAccounts`,
  `getTokenAccountBalance`, `getAddressLookupTable` fetch + decode), transactions and blocks
  (`getTransaction`, `getSignaturesForAddress`, `getSignatureStatuses`, `getBlock`, `getBlockHeight`,
  `getTransactionCount`, `getFeeForMessage`), and cluster state (`getBalance`, `getSlot`,
  `getLatestBlockhash`, `isBlockhashValid`, `getEpochInfo`, `getVersion`, `getHealth`, `getSupply`,
  `getSlotLeaders`, `getRecentPrioritizationFees`, `getTokenSupply`, `getMinimumBalanceForRentExemption`,
  `getVoteAccounts`, `getInflationReward`, `getLeaderSchedule`, `getBlocks`, `getClusterNodes`,
  `requestAirdrop`); each typed, fully documented, and tested.
- Account-state decoders — `Mint` and `TokenAccount` (SPL Token state, via `GetMintAsync` /
  `GetTokenAccountAsync`) and `AddressLookupTable`; for other programs, pair `getAccountInfo` with Core's
  `BorshReader`.
- `getTransaction` returns the decoded transaction bytes (feed to `Transaction.Deserialize`) alongside rich
  metadata — pre/post SOL and token balances, inner (CPI) instructions, loaded lookup-table addresses, logs,
  and compute units. Failures decode to a typed `TransactionError` (exposing the program's `Custom` code) on
  `TransactionMeta`, `SignatureStatus`, and `SimulateTransactionResult`.
- `GetParsedTransactionAsync` / `GetParsedBlockAsync` / `GetParsedAccountInfoAsync` return the node's
  `jsonParsed` decoding — typed instructions, token balances, account state, and logs without local Borsh
  work — each instruction keeping both its parsed form and its raw program id / accounts / data.
- WebSocket streaming multiplexed over one connection: `SubscribeSlotsAsync` and `SubscribeRootsAsync`
  (`IAsyncEnumerable`), `SubscribeLogsAsync`, `SubscribeAccountAsync`, `SubscribeParsedAccountAsync`, `SubscribeProgramAsync`,
  `SubscribeSignatureAsync`, `SubscribeBlocksAsync`, and `SubscribeParsedBlocksAsync` (`ChannelReader`), with
  automatic reconnect and resubscribe across dropped connections.
- DI registration with a built-in resilience pipeline (retry on transient errors and HTTP 429).
- `SendTransactionAsync` / `SimulateTransactionAsync` — submit a signed transaction or dry-run it for logs and
  compute units; `SendAndConfirmTransactionAsync` sends and waits for confirmation (throwing if the transaction
  lands but errors). Confirm by polling (`GetSignatureStatusesAsync` / `ConfirmTransactionAsync`) or over the
  WebSocket (`SolanaWsClient.ConfirmSignatureAsync`).

```csharp
using SolSharp.Rpc;

// typed client with retries; tune the pipeline via the optional callback
services.AddSolanaRpc("https://your-rpc-endpoint");

// injected SolanaRpcClient
var lamports = await rpc.GetBalanceAsync(account);

// streaming
await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://your-rpc-endpoint"));
await foreach (var slot in ws.SubscribeSlotsAsync())
    Console.WriteLine(slot.Slot);
```

`SolSharp.Wallet`:

- `Keypair` — generate a key, or load one with `Parse` (auto-detecting a base58 export, a `solana-keygen`
  JSON array, hex, or base64); signs messages and zeroes its secret on dispose (or finalization).
- `ISigner` — the signing abstraction the transaction builder depends on, so the key stays swappable.
- `PublicKey.Verify(message, signature)` — Ed25519 verification, kept in Wallet so Core stays crypto-free.

```csharp
using SolSharp.Wallet;

using var keypair = Keypair.Generate();      // or Keypair.Parse(phantomExport / id.json)
byte[] signature = keypair.Sign(message);
bool ok = keypair.PublicKey.Verify(message, signature);
```

`SolSharp.Programs`:

- Instruction builders: `SystemProgram` (transfer, create / allocate / assign, create-with-seed, durable nonce), `ComputeBudgetProgram` (compute-unit
  limit and priority fee), `TokenProgram` (transfer / transfer-checked, mint / burn, approve / revoke,
  freeze / thaw, initialize mint / account, close, sync-native — each with a `tokenProgram` override for
  Token-2022), `AssociatedTokenAccount`, `AddressLookupTableProgram` (create / extend / deactivate / close),
  and `MemoProgram`.
- `ProgramDerivedAddress` (`FindProgramAddress` / `TryCreateProgramAddress`) and `PublicKey.IsOnCurve()`.
- `Message` (legacy) and `MessageV0` (versioned, loading extra accounts from address lookup tables),
  `Transaction`, and `TransactionBuilder` (`Build` / `BuildV0`) — compilation, wire serialization (with
  `Transaction.Deserialize` to parse one back, and `DecompileInstructions` to resolve a parsed message's
  instructions to program ids and account keys, loading v0 lookup-table accounts), signing, and base64
  output. Every encoding is checked byte-for-byte against the Rust `solana-sdk` (via solders) and `solana-py`.

```csharp
using SolSharp.Programs;
using SolSharp.Wallet;

using var payer = Keypair.Parse(secret);
var blockhash = (await rpc.GetLatestBlockhashAsync()).Blockhash;

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(ComputeBudgetProgram.SetComputeUnitPrice(50_000))
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1_000_000))
    .Build(payer);

var signature = await rpc.SendTransactionAsync(tx.Serialize());
```

## Roadmap

- [x] Core primitives — `PublicKey`, `Base58`, `ShortVec`
- [x] RPC enum + JSON converters (`Commitment`)
- [x] Program / sysvar / mint constants + validation
- [x] `SolSharp.Wallet` — Ed25519 keys, signing/verification, key parsing
- [x] `SolSharp.Rpc` — HTTP reads (`getAccountInfo` / `getMultipleAccounts` / `getProgramAccounts` / `getSignaturesForAddress`, balances, blockhash, token supply, ...) + `sendTransaction` / `simulateTransaction`; multiplexed WebSocket streaming (slots, logs, accounts, programs, signatures, blocks) with auto-reconnect and optional `ILogger` diagnostics; DI + resilience
- [x] `SolSharp.Programs` — System / Token (+ Token-2022) / ATA / Compute Budget / Memo instructions, PDA/ATA, transaction builder
- [x] Versioned (v0) transactions + address lookup tables (compile / sign / fetch + decode / ALT program)
- [x] Borsh reader + writer, typed SPL account state (`Mint` / `TokenAccount`), `Transaction.Deserialize` + instruction decompilation, and typed `TransactionError`
- [x] Live integration test suite (configurable RPC / WS endpoint)
- [x] Published NuGet package (single `SolSharp` package bundling the four assemblies)

## Requirements

- .NET 8 SDK or later.

## Build & test

```bash
dotnet build
dotnet test
dotnet format   # apply the enforced code style
```

The suite includes a `SolSharp.IntegrationTests` project that exercises the read and streaming paths against a
live cluster. It targets the public mainnet endpoint by default and is overridable via the `SOLSHARP_RPC_URL`
and `SOLSHARP_WS_URL` environment variables; no credentials are committed. These tests hit the network, so they
tolerate rate limits by reporting inconclusive rather than failing, and are tagged `Integration`. For a fast,
offline-only run, exclude them:

```bash
dotnet test --filter "TestCategory!=Integration"
```

To point the integration tests at your own node, set the endpoints (the key stays in your shell, never the repo):

```bash
SOLSHARP_RPC_URL=https://your-node SOLSHARP_WS_URL=wss://your-node \
  dotnet test --filter "TestCategory=Integration"
```

## Layout

```
SolSharp/
  src/SolSharp.Core/   Encoding/  Primitives/  Converters/  Constants/
  src/SolSharp.Rpc/    Protocol/  Models/  Streaming/  + client, options, DI
  src/SolSharp.Wallet/ Keypair (+ parsing), ISigner, PublicKey.Verify / IsOnCurve
  src/SolSharp.Programs/ instructions, PDA/ATA, Message/Transaction, TransactionBuilder
  src/SolSharp/        packaging facade — bundles the four assemblies into the single NuGet package
  tests/               NUnit + FluentAssertions, mirroring each project
                       (+ SolSharp.IntegrationTests: live-cluster read/streaming checks)
  .github/workflows/   ci.yml (build + offline tests) and release.yml (tag → NuGet trusted publishing)
  assets/              package icon and README logo
  .editorconfig        modern C# style, enforced on build
  Directory.Build.props
  CLAUDE.md            conventions and decisions for contributors/agents
  docs/USAGE.md        task-oriented usage guide with runnable examples
```

## Design notes

- `Core` is dependency-light and free of I/O and crypto by design — anything that needs the
  network or Ed25519 lives in a higher layer.
- Wire formats and signing are money-critical: they are validated against known-good vectors,
  not just round-trips.
- Conventions, layering rules, and design decisions are documented in [`CLAUDE.md`](CLAUDE.md).

## Security

SolSharp handles private keys and builds transactions that move funds. It has **not** been
audited — use at your own risk. Never commit secrets or private keys, and never hand a raw
private key to a dependency you do not control: build with a third-party library if you must,
but sign with your own signer and simulate before sending.

## License

[MIT](LICENSE) © Yevhen Koval
