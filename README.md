<p align="center">
  <img src="https://raw.githubusercontent.com/jecacs/SolSharp/main/assets/logo.png" alt="SolSharp" width="180" />
</p>

# SolSharp

[![NuGet](https://img.shields.io/nuget/v/SolSharp.svg?logo=nuget)](https://www.nuget.org/packages/SolSharp)
[![Downloads](https://img.shields.io/nuget/dt/SolSharp.svg?logo=nuget)](https://www.nuget.org/packages/SolSharp)
[![build](https://github.com/jecacs/SolSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/jecacs/SolSharp/actions/workflows/ci.yml)
[![Security checks](https://github.com/jecacs/SolSharp/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/jecacs/SolSharp/actions/workflows/security.yml?query=branch%3Amain)
[![CodeQL](https://github.com/jecacs/SolSharp/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/jecacs/SolSharp/actions/workflows/codeql.yml?query=branch%3Amain)
[![Unit test coverage](https://img.shields.io/badge/unit_test_coverage-93.7%25_line-brightgreen)](#quality-gates)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/jecacs/SolSharp/badge)](https://scorecard.dev/viewer/?uri=github.com/jecacs/SolSharp)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A modern, contract-driven, Native AOT-ready .NET SDK for Solana — keys and signatures,
program instructions, transaction wire formats, RPC, and WebSocket streaming. SolSharp is
independently implemented in C# from pinned Anza Solana SDK, Agave, and SPL source contracts;
money-critical encodings are checked against exact upstream-compatible vectors. No reflection
is used by the library: JSON is source-generated, all four functional assemblies declare Native AOT
compatibility, and CI native-publishes and runs a consumer of the packed NuGet artifact.

SolSharp is built for low latency with focused dependencies and a dependency-light Core. It exposes the protocol
instead of hiding it: typed byte-level values, explicit signing, exact instruction layouts,
bounded codecs, and typed network responses. If you are
writing wallets, bots, indexers, or backend services that talk to Solana from .NET and care
about correctness, speed, and control, this is aimed at you.

> **Status: 2.0.0.** SolSharp ships as a single NuGet package — `SolSharp` —
> bundling the Core (primitives + encodings), Wallet (Ed25519 and BLS12-381 keys, signing, verification,
> key import/export, BIP-39/SLIP-0010 derivation, and signed off-chain messages), Rpc (the full applicable
> non-admin JSON-RPC HTTP surface + WebSocket streaming + DI), and
> Programs (instructions + transaction building + signing, durable nonces) assemblies. JSON is
> source-generated and all four functional assemblies are Native AOT compatible. Versioning follows semver: from 1.0.0,
> breaking changes only come with a major version.

📖 **New here? Read the [usage guide](docs/USAGE.md)** — a task-oriented cookbook covering keys, reads,
SPL token state, building/signing/sending transactions, v0 + address lookup tables, SIMD-0385 V1, decoding transactions,
WebSocket subscriptions, confirmation, Native AOT publishing, and more.

## Motivation

When this was started, the .NET options for Solana were either unmaintained and stale or
heavy and not built for performance — there was no modern, fast, actively-developed client.
SolSharp is an independently written C# 12 answer: allocation-conscious, tuned for
latency-sensitive workloads, and engineered from the Rust implementations that define the
network's actual wire behavior rather than from prose documentation alone.

## Upstream provenance and parity

Compatibility work is tied to immutable source revisions. The
[Rust parity matrix](docs/RUST_PARITY.md) records the exact Anza Solana SDK, Agave, System,
Address Lookup Table, SPL Token, Token-2022, and Associated Token Account commits used for
each contract, what SolSharp currently covers, and what is intentionally outside a client
SDK. Tests use upstream known-answer vectors or independently generated compatible vectors;
a C# round trip alone is not treated as proof of wire compatibility.

SolSharp is not a generated binding, a fork, or an official Anza/Solana product. It is an
idiomatic C# implementation whose public client behavior is verified against the pinned Rust
sources. See [third-party notices](THIRD_PARTY_NOTICES.md) for attribution and licensing.

## Why

- **Native AOT ready.** JSON is source-generated (no reflection anywhere), every assembly is trimmable
  and AOT-clean, and CI publishes and runs a native-compiled smoke sample on every push and pull request
  targeting `main`. Ship your bot
  as a self-contained native binary with instant startup.
- **Lean.** No kitchen-sink dependency graph. `Core` depends on a single package (base58).
- **Wire-level control.** Hand-written, bounds-checked transaction, message, account, and instruction
  codecs — the part most SDKs hide — with Ed25519 signing on a vetted crypto library and exact
  vectors derived from the pinned Rust contracts.
- **Latency-minded.** Value types, allocation-free hot paths, span-based APIs.
- **Modern .NET 8.** C# 12, nullable reference types, code style enforced on build.

## How it compares to Solnet

[Solnet](https://github.com/bmresearch/Solnet) is the longest-standing .NET SDK for Solana and has a
valuable ecosystem-oriented program surface. SolSharp is independently written with a different target:
application-side parity with immutable official Rust contracts, plus a verifiable .NET deployment story.
The official Rust column below is the reference contract rather than another client implementation.

Comparison basis: SolSharp is release `2.0.0`; Solnet means its
[published `8.7.0` release](https://github.com/bmresearch/Solnet/commit/e8df87bdb2006376ba3eea9e1d3b857c84fc5685)
(2025-11-26), with unreleased-head differences called out explicitly; the Rust reference is the
[pinned Anza SDK/Agave/SPL matrix](docs/RUST_PARITY.md).

| Dimension | Official Rust SDK / Agave reference | SolSharp 2.0.0 | Solnet official packages/source |
| --- | --- | --- | --- |
| **Transaction formats** | Legacy, V0, and feature-gated [SIMD-0385 V1](https://github.com/anza-xyz/solana-sdk/blob/ec7a0467e268774b724d55120ad952b518f27d64/message/src/versions/v1/message.rs), including inline V1 configuration and a message-first signature envelope | Legacy/V0/V1 build, sanitize, parse, sign, serialize, and decompile; exact V1 config/framing and envelope vectors | Published 8.7: Legacy/V0 and [rejects versions above 0](https://github.com/bmresearch/Solnet/blob/e8df87bdb2006376ba3eea9e1d3b857c84fc5685/src/Solnet.Rpc/Models/Message.cs#L275-L286). Unreleased head names V1, but its current body/envelope is not the pinned SIMD-0385 layout (details below) |
| **Native and SPL clients** | Canonical native-program and SPL interface crates, split by contract | System, Stake, Vote, legacy/upgradeable/V4 loaders, Compute Budget, ALT, Memo, three signature precompiles; Token, Token-2022 extensions/interfaces, ATA, metadata/group/transfer-hook, and ElGamal proof/registry client contracts with typed decoders | Broader ecosystem-oriented set including Governance, Stake Pool, Token Swap, Account Compression, Name Service, and Shared Memory; repository head adds an initial Token-2022 surface |
| **HTTP RPC** | [53 applicable non-admin, non-obsolete request variants](https://github.com/anza-xyz/agave/blob/ab6553293094e59dee7d3e7c928c7fa1023d0684/rpc-client-types/src/request.rs#L12-L75) in the pinned Agave client enum | 53/53 typed async methods, including current context-slot, filter, slice, encoding/detail/reward, raw/parsed V1, and context-wrapped response variants; batching, bounded responses, typed errors, and send/simulate/confirm | 50/53 pinned methods through sync/async `RequestResult<T>` APIs; no `getAgGenesisCert`, `getRecentPrioritizationFees`, or `getStakeMinimumDelegation` at the examined head |
| **PubSub** | Nine families: account, program, logs, signature, slot, slots-updates, block, vote, and root | 9/9, including exact logs/block filter unions, parsed account/program forms, early signature-receipt events, bounded channels, cancellation isolation, reconnect/replay, and V1 block opt-ins | [Six families](https://github.com/bmresearch/Solnet/blob/ebec9e1a3b708dbe86d103dd8fcf869d0cd923b6/src/Solnet.Rpc/IStreamingRpcClient.cs): account, program, logs, signature, slot, and root |
| **Offline / multisig signing** | Signer, presigner/null-signer, partial signing, fixed signature slots, and per-slot verification primitives | Exact message-byte export/hash, typed fixed slots, partial/all signing, verified external insertion, `Presigner`, `NullSigner`, and SPL multisig builders | Partial signing, externally supplied signatures, and program multisig builders/examples |
| **AOT / trimming** | Native Rust output; not a .NET compatibility contract | Every assembly declares `IsAotCompatible`; generated JSON metadata, trim/AOT analyzers, and CI that publishes and runs a native package consumer | Targets .NET 8, but the examined projects publish no solution-wide AOT/trimming declaration or native-publish CI contract; reflection paths remain |
| **Packaging** | Modular Cargo crates | One NuGet package containing four compiler-layered functional assemblies plus a minimal packaging facade | Five installable packages: `Solana.Rpc`, `Solana.Wallet`, `Solana.Programs`, `Solana.Extensions`, and `Solana.KeyStore` |
| **Reproducibility** | The authoritative source itself | Seven immutable upstream revisions plus exact byte/KAT tests tied to named Rust/RFC/BIP/SLIP vectors | Own unit/RPC fixtures; published documentation has no equivalent immutable upstream revision matrix |

Solnet's unreleased [`ebec9e1` head](https://github.com/bmresearch/Solnet/commit/ebec9e1a3b708dbe86d103dd8fcf869d0cd923b6)
contains `MessageV1`, but the examined [message](https://github.com/bmresearch/Solnet/blob/ebec9e1a3b708dbe86d103dd8fcf869d0cd923b6/src/Solnet.Rpc/Models/Message.cs#L256-L524)
and [transaction](https://github.com/bmresearch/Solnet/blob/ebec9e1a3b708dbe86d103dd8fcf869d0cd923b6/src/Solnet.Rpc/Models/Transaction.cs#L265-L290)
still use a V0-shaped body and signatures-first envelope. That is why the table does not count it as parity
with the pinned Rust [V1 envelope](https://github.com/anza-xyz/solana-sdk/blob/ec7a0467e268774b724d55120ad952b518f27d64/transaction/src/versioned/mod.rs#L345-L390).

Choose **Solnet** when its broader ecosystem integrations or modular package topology match the application.
Choose **SolSharp** when exact pinned native/SPL wire contracts, complete pinned RPC/PubSub coverage,
Native AOT, bounded hostile-input behavior, and reproducible upstream parity are the primary constraints.

## Package

SolSharp ships as a **single NuGet package** — `SolSharp` — so one `dotnet add package SolSharp` pulls in
everything. Internally it stays four layered functional assemblies plus a minimal packaging facade,
bundled into that one package (namespaces are
unchanged: `SolSharp.Core.*`, `SolSharp.Rpc`, `SolSharp.Wallet`, `SolSharp.Programs`):

Install from [NuGet](https://www.nuget.org/packages/SolSharp):

```bash
dotnet add package SolSharp
```

```xml
<PackageReference Include="SolSharp" Version="2.0.0" />
```

| Assembly           | Purpose                                              |
| ------------------ | ---------------------------------------------------- |
| `SolSharp.Core`    | Primitives, encoding, JSON, program/sysvar constants |
| `SolSharp.Wallet`  | Ed25519/BLS keys, secure import/export, signing, verification, and off-chain messages |
| `SolSharp.Rpc`     | Full applicable non-admin HTTP JSON-RPC surface + bounded, auto-reconnecting WebSocket streaming + DI |
| `SolSharp.Programs`| Native/SPL instructions and state decoders + legacy/v0/V1 transaction building |

Keeping the split in the source means the layering stays compiler-enforced — dependencies point downward
only: `Rpc` and `Wallet` build on `Core`, and `Programs` builds on `Core` and `Wallet`. `Core` depends on
nothing else in the solution and pulls no network or crypto package.

See the [changelog](CHANGELOG.md) for what changed in each release.

## What's inside

`SolSharp.Core`:

- `PublicKey` and `Hash` — distinct 32-byte value types with value equality, base58 parsing, byte-copy APIs,
  and source-generated JSON support.
- `Base58`, `ShortVec` (compact-u16), and `BorshReader` / `BorshWriter` — the encodings Solana uses on the
  wire, plus a bounds-checked reader and writer for Anchor / Borsh account data and instruction arguments.
- `Commitment` — an RPC enum that serializes to its exact wire string.
- `SolanaProgramIds`, `Sysvars`, `SolanaFeatureIds`, and `Mints` — well-known
  on-chain addresses, guarded by tests that every constant decodes to a valid 32-byte key.
- Bounded current sysvar states for Clock, Rent, EpochSchedule, EpochRewards, LastRestartSlot,
  SlotHashes, SlotHistory, and StakeHistory.
- `SolanaUnits` — SOL ↔ lamports conversion.

```csharp
using SolSharp.Core.Primitives;

var mint = PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");
byte[] raw = mint.ToBytes();              // new 32-byte copy
bool ok = PublicKey.TryParse(input, out var key);
```

`SolSharp.Rpc`:

- HTTP JSON-RPC methods — the full applicable non-admin surface from the pinned Agave revision: accounts (`getAccountInfo`,
  `getMultipleAccounts`, `getProgramAccounts` with the complete base58/base64/raw memcmp, unsigned data-size,
  token-account-state filter union and data slices,
  `getTokenAccountsByOwner`, `getTokenAccountsByDelegate`, `getTokenLargestAccounts`,
  `getTokenAccountBalance`, `getLargestAccounts`, plus the `GetAddressLookupTableAsync` helper
  (fetch + decode through `getAccountInfo`)),
  transactions and blocks (`getTransaction`, `getSignaturesForAddress`, `getSignatureStatuses`,
  `getBlock`, `getBlockHeight`, `getBlockTime`, `getBlockCommitment`, `getBlockProduction`,
  `getTransactionCount`, `getFeeForMessage`), and cluster state (`getBalance`, `getSlot`,
  `getLatestBlockhash`, `isBlockhashValid`, `getEpochInfo`, `getEpochSchedule`, `getVersion`,
  `getHealth`, `getIdentity`, `getGenesisHash`, `getAgGenesisCert`, `getSupply`, `getSlotLeader`,
  `getSlotLeaders`,
  `getRecentPrioritizationFees`, `getRecentPerformanceSamples`, `getTokenSupply`,
  `getMinimumBalanceForRentExemption`, `getVoteAccounts`, `getInflationReward`,
  `getInflationGovernor`, `getInflationRate`, `getLeaderSchedule`, `getBlocks`,
  `getBlocksWithLimit`, `getFirstAvailableBlock`, `getClusterNodes`, `getHighestSnapshotSlot`,
  `getMaxRetransmitSlot`, `getMaxShredInsertSlot`, `getStakeMinimumDelegation`,
  `minimumLedgerSlot`, `requestAirdrop`); each typed, fully documented, and tested.
  Full configuration methods preserve upstream `minContextSlot`, context-wrapped account scans,
  mint/program-id token filters, data slices, sorting, airdrop blockhashes, and the closed `RpcAccountData`
  union: legacy binary, tagged base58/base64, parsed JSON with its unknown-program base64 fallback, and
  `base64+zstd`. Raw transaction/block encoding, detail, and rewards choices remain explicit without
  weakening the convenient defaults.
- Account-state decoders — `Mint` and `TokenAccount` (SPL Token state, via `GetMintAsync` /
  `GetTokenAccountAsync`), `NonceAccount` (via `GetNonceAccountAsync`), `AddressLookupTable`, and the
  Token-2022 extension section (`TokenExtensionSet` — TLV walking plus typed views for transfer fees,
  metadata pointer / in-mint metadata, permanent delegate, and more); for other programs, pair
  `getAccountInfo` with Core's `BorshReader`.
- `getTransaction` returns the decoded transaction bytes (feed to `Transaction.Deserialize`) alongside rich
  metadata — transaction version/index, pre/post SOL and token balances, inner (CPI) instructions, loaded
  lookup-table addresses, logs, compute/cost units, program return data, and rewards. Failures decode to a
  typed `TransactionError` (including parameterized runtime errors and the program's `Custom` code) on
  `TransactionMeta`, `SignatureStatus`, and `SimulateTransactionResult`. The compatibility-preserving default
  read advertises legacy/v0; `GetTransactionWithMaxVersionAsync(..., 1)` opts into V1 bytes, which
  `Transaction.Deserialize` understands locally.
- `GetParsedTransactionAsync` / `GetParsedBlockAsync` / `GetParsedAccountInfoAsync` return the node's
  `jsonParsed` decoding — typed instructions, token balances, account state, and logs without local Borsh
  work. Recognized instructions carry the node's parsed action; unrecognized instructions retain their raw
  program id, account list, and base58 data, matching the upstream tagged response union. Explicit
  `*WithMaxVersionAsync` variants opt parsed transaction/block reads into V1 and preserve its inline
  `transactionConfig`.
- WebSocket streaming multiplexed over one connection: `SubscribeSlotsAsync`, `SubscribeRootsAsync`,
  `SubscribeSlotsUpdatesAsync` (slot lifecycle with per-stage stats), and `SubscribeVotesAsync` (gossip
  votes) as `IAsyncEnumerable`; `SubscribeLogsAsync`, `SubscribeAccountAsync`, `SubscribeParsedAccountAsync`,
  `SubscribeProgramAsync`, `SubscribeSignatureAsync`, `SubscribeBlocksAsync`, and `SubscribeParsedBlocksAsync`
  (`ChannelReader`) — with automatic reconnect and resubscribe across dropped connections, and a bounded
  transport (message-size cap, per-subscription buffers, opt-in receive timeout). The source-safe
  `SubscribeAccountWithOptionsAsync` and `SubscribeProgramWithOptionsAsync` paths expose the effective
  encoding/commitment fields (plus program filters) and return the same exact `RpcAccountData` union as HTTP.
  Agave-accepted subscription fields that its encoder ignores are deliberately not advertised. Block
  subscriptions also provide explicit `*WithMaxVersionAsync` V1 opt-ins; full methods cover logs/block filter
  unions, parsed program streams, and the optional early `receivedSignature` event before final processing.
- DI registration with a built-in resilience pipeline (retry on transient errors and HTTP 429), plus
  `AddSolanaWs` for a container-managed streaming client.
- JSON-RPC batching — `CreateBatch()` queues reads (and sends) and submits them in one HTTP round-trip.
- `SendTransactionAsync` / `SimulateTransactionAsync` — submit a signed transaction or dry-run it for logs,
  compute units, account snapshots, parsed inner instructions, balances, fees, and return data; both run
  preflight/simulation at `confirmed` by default to match `GetLatestBlockhashAsync`;
  `SendAndConfirmTransactionAsync` sends and waits for confirmation (throwing if the transaction
  lands but errors). Confirm by polling (`GetSignatureStatusesAsync` / `ConfirmTransactionAsync`) or over the
  WebSocket (`SolanaWsClient.ConfirmSignatureAsync`).

```csharp
using SolSharp.Rpc;
using SolSharp.Rpc.Streaming;

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
  JSON array, hex, or base64); export the Rust/wallet 64-byte, base58, or `id.json` forms deliberately;
  signs messages and zeroes its stored seed on dispose (or finalization).
- `Signature` — a typed 64-byte base58 value with strict verification; `Presigner` validates externally
  produced signatures against the requested message, while `NullSigner` represents an absent offline cosigner.
- `BlsKeypair`, `BlsPublicKey`, `BlsSignature`, and `BlsProofOfPossession` — the pinned minimal-public-key-size
  BLS12-381 proof-of-possession scheme used by current Vote v2/v4 contracts, with subgroup/infinity validation,
  Rust-compatible binary/zeroable UTF-8 JSON key files, strict fixed-size base64 text, and vote-account-bound
  proofs that typed Vote builders verify locally before serialization. Signature verification is available only
  through a derived keypair or `BlsPopVerifiedPublicKey`; `BlsAggregatePublicKey` likewise admits only PoP-verified
  keys for safe same-message aggregation.
- `OffchainMessage` — the pinned SDK's version-0, domain-separated signed-message format with canonical
  ASCII/UTF-8 selection, strict parsing, typed hashes, signing, and verification.
- Mnemonic import — `FromMnemonic` (the `solana-keygen` scheme) and `FromMnemonicAtPath` (the
  Phantom / Solflare SLIP-0010 scheme), built on the public `Bip39` and `Slip10` helpers and validated
  against the official test vectors.
- `ISigner` — the signing abstraction the transaction builder depends on, so the key stays swappable.
- `PublicKey.Verify(message, signature)` — Solana-compatible strict Ed25519 verification (including rejection
  of small-order public keys and signature points), kept in Wallet so Core stays crypto-free.

```csharp
using SolSharp.Wallet;

using var keypair = Keypair.Generate();      // or Keypair.Parse(phantomExport / id.json)
using var wallet = Keypair.FromMnemonicAtPath(words, "m/44'/501'/0'/0'"); // Phantom-style import
byte[] signature = keypair.Sign(message);
bool ok = keypair.PublicKey.Verify(message, signature);
```

`SolSharp.Programs`:

- Native instruction clients: the current System and durable-nonce helpers; Stake and Vote (including
  compact/tower and V2/BLS forms); legacy, upgradeable, and V4 loaders; Compute Budget; Address Lookup
  Tables; Feature Gate; Memo; and self-contained/cross-instruction Ed25519, Secp256k1, and Secp256r1 verification.
- SPL clients: the pinned classic Token family (checked, multisig, batch, and newer interface helpers),
  ATA create/idempotent/recover-nested, and Token-2022 base plus transfer-fee, default-state, memo/CPI,
  pointer, interest/scaled, pausable, permissioned-burn, metadata, token-group, transfer-hook,
  confidential-transfer/fee/mint-burn, native proof-program, and ElGamal registry contracts.
- Typed, bounds-checked decoders cover native/SPL account state and Token/Token-2022 instruction
  discriminators, including nonce, stake, versioned vote, loader, ALT, Instructions sysvar, Token-2022 TLV, metadata,
  token-group, transfer-hook metadata, and ElGamal registry data. Confidential builders consume exact
  caller-generated POD/ciphertext/proof bytes;
  SolSharp does not pretend to generate or verify zero-knowledge proofs off chain.
- `ProgramDerivedAddress` (`FindProgramAddress` / `TryCreateProgramAddress` / System `CreateWithSeed`) and
  `PublicKey.IsOnCurve()`.
- `Message` (legacy), `MessageV0` (loading extra accounts from address lookup tables), and SIMD-0385
  `MessageV1` (inline execution configuration and fixed-width instruction framing), plus `Transaction` and
  `TransactionBuilder` (`Build` / `BuildV0` / `BuildV1`, and matching `BuildMessage*` methods for the
  unsigned message, durable-nonce anchoring via `SetDurableNonce`) — compilation, wire serialization (allocation-free via `Transaction.TrySerialize` and
  the span `Serialize` overloads, with
  `Transaction.Deserialize` to parse one back — enforcing Solana's sanitize rules on malformed input — and
  `DecompileInstructions` to resolve a parsed message's
  instructions to program ids and account keys, loading v0 lookup-table accounts), version-aware signing, and base64
  output. Offline coordination is explicit through typed `Signatures` / `RequiredSignerKeys`, `PartialSign`,
  verified `AddSignature`, `SignAll`, per-slot verification, and `IsFullySigned`. `TransactionMessageHash`
  exposes the Rust SDK's domain-separated BLAKE3 message identifier.
  Money-critical encodings are checked byte-for-byte against pinned Rust or independently generated
  compatible vectors, not only against local round trips.

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

## Requirements

- .NET 8 SDK or later. `global.json` selects the lowest available compatible major beginning at
  .NET 8, so CI proves the minimum while newer local SDKs remain usable.
- Calling the BLS12-381 API requires one of the native RIDs shipped by `Nethermind.Crypto.Bls` 1.0.5:
  `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, or `win-x64`. All non-BLS SolSharp APIs remain
  managed and do not load that native backend.

## Build & test

```bash
dotnet build
dotnet test
dotnet format   # apply the enforced code style
```

The suite includes a `SolSharp.IntegrationTests` project that exercises the read and streaming paths against a
live cluster, plus a write suite (airdrop, transfer, durable nonce) that always targets **devnet**. Reads
default to the public mainnet endpoint (`SOLSHARP_RPC_URL` / `SOLSHARP_WS_URL` override); the write suite uses
the public devnet endpoint (`SOLSHARP_DEVNET_RPC_URL` override); no credentials are committed. These tests hit
the network, so they tolerate rate limits by reporting inconclusive rather than failing, and are tagged
`Integration`. Live HTTP reads and devnet writes use two-request-per-second test-only limiters; WebSocket
probes run serially with starts spaced by 500 ms. The shared faucet can still reject `requestAirdrop`
independently of RPC traffic. For a fast, offline-only run, exclude them:

```bash
dotnet test --filter "TestCategory!=Integration"
```

Micro-benchmarks live in a standalone BenchmarkDotNet harness:

```bash
dotnet run -c Release --project benchmarks/SolSharp.Benchmarks
```

To point the integration tests at your own node, set the endpoints (the key stays in your shell, never the repo):

```bash
SOLSHARP_RPC_URL=https://your-node SOLSHARP_WS_URL=wss://your-node \
  dotnet test --filter "TestCategory=Integration"
```

## Layout

```
SolSharp/
  src/SolSharp.Core/   Encoding/  Primitives/  Converters/  Constants/  SysvarStates/
  src/SolSharp.Rpc/    Protocol/  Models/  Streaming/  + client, options, DI
  src/SolSharp.Wallet/ Ed25519/BLS keys, signers, verification, off-chain messages
  src/SolSharp.Programs/ native/SPL clients and states, PDA/ATA, messages and transactions
  src/SolSharp/        packaging facade — bundles the four assemblies into the single NuGet package
  samples/             SolSharp.AotSmoke — native-compiled smoke sample, published and run in CI
  tests/               NUnit + FluentAssertions + bounded FsCheck properties, mirroring each project
                       (+ SolSharp.IntegrationTests: live-cluster read/streaming checks)
  benchmarks/          SolSharp.Benchmarks — standalone BenchmarkDotNet micro-benchmark harness
  .github/workflows/   CI, coverage, dependency/security review, Scorecard, and trusted publishing
  assets/              package icon and README logo
  .editorconfig        modern C# style, enforced on build
  global.json          .NET 8 minimum policy with local roll-forward (CI asserts SDK 8)
  Directory.Build.props
  THIRD_PARTY_NOTICES.md exact compatibility pins and native BLS attribution
  CLAUDE.md            conventions and decisions for contributors/agents
  docs/USAGE.md        task-oriented usage guide with runnable examples
  docs/RUST_PARITY.md  pinned Rust/Agave/SPL client-contract coverage matrix
```

## Quality gates

The four unit-test suites' reproducible .NET 8 Linux coverage baseline across `SolSharp.Core`,
`SolSharp.Rpc`, `SolSharp.Wallet`, and `SolSharp.Programs` is
**93.7% of lines**. Build outputs under `obj/**` and generated `*.g.cs` pseudo-sources are excluded;
overlapping lower-layer hits are merged rather than counted twice. CI publishes the merged Cobertura
line/branch and Markdown reports, fails if repository-wide line coverage drops below 90%, and rejects a
documented percentage that exceeds the current measured result.

Five deterministic FsCheck properties exercise 5,000 generated hostile-input cases on every CI run:
bounded arbitrary transaction payloads, proper prefixes, and single-byte overwrite cases across legacy,
v0, and V1. Accepted payloads must reserialize byte-for-byte; rejected payloads must fail with the
documented format error rather than an unexpected exception.

Every pull request also receives a direct-and-transitive NuGet advisory audit and a dependency-diff
review. All ordinary CI/release restores use committed `packages.lock.json` files in locked mode; the
dynamic packed-package AOT consumer uses an isolated generated lock and independently verifies the exact
package artifact. CodeQL runs the `security-extended` C# query suite, the dependency audit runs weekly even
without repository changes, and OpenSSF Scorecard checks the repository's supply-chain posture. A release
publishes the verified `.nupkg`, its SHA-256 digest, and Sigstore/SLSA build provenance together on GitHub.
A green security badge means those automated checks found no known issue at the tested revision; it is not
a claim that the library is vulnerability-free or a substitute for an independent audit.

## Design notes

- `Core` is dependency-light and free of I/O and crypto by design — anything that needs the
  network or Ed25519 lives in a higher layer.
- Wire formats and signing are money-critical: they are validated against known-good vectors,
  not just round-trips.
- Conventions, layering rules, and design decisions are documented in [`CLAUDE.md`](CLAUDE.md).

## Security

SolSharp handles private keys and builds transactions that move funds. It has **not** been
audited — use at your own risk. Never commit secrets or private keys, and never export a raw
private key to an RPC provider, hosted service, or third-party transaction builder. Keep signing
behind `ISigner`, inspect and simulate externally built transactions, then send only signed bytes.

To report a vulnerability, see the [security policy](SECURITY.md) — please use private reporting
rather than a public issue.

## License

[MIT](LICENSE) © Yevhen Koval
