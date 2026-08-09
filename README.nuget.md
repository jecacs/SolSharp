# SolSharp

[![Security checks](https://github.com/jecacs/SolSharp/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/jecacs/SolSharp/actions/workflows/security.yml?query=branch%3Amain)
[![CodeQL](https://github.com/jecacs/SolSharp/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/jecacs/SolSharp/actions/workflows/codeql.yml?query=branch%3Amain)
[![Unit test coverage](https://img.shields.io/badge/unit_test_coverage-93.7%25_line-brightgreen)](https://github.com/jecacs/SolSharp/blob/v2.0.0/README.md#quality-gates)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/jecacs/SolSharp/badge)](https://scorecard.dev/viewer/?uri=github.com/jecacs/SolSharp)

A modern, contract-driven, Native AOT-ready .NET SDK for Solana — keys and signatures,
program instructions, transaction wire formats, RPC, and WebSocket streaming. SolSharp is
independently implemented in C# from pinned Anza Solana SDK, Agave, and SPL source contracts.
No reflection is used by the library: JSON is source-generated, all four functional assemblies declare
Native AOT compatibility, and CI native-publishes and runs a consumer of the packed package.

SolSharp is built for low latency with focused dependencies and a dependency-light Core. If you are writing
bots, indexers, or backend services that talk to Solana from .NET and care about speed
and control, this is aimed at you.

## Why SolSharp

- **Native AOT ready.** Source-generated JSON (no reflection), trimmable, AOT-clean — ship a
  self-contained native binary with instant startup. CI runs a native-compiled smoke test on every push
  and pull request targeting `main`.
- **Full pinned RPC coverage.** The complete applicable non-admin JSON-RPC HTTP surface from the pinned Agave revision, including reads, send/simulate, airdrop,
  batching, and multiplexed WebSocket subscriptions with automatic reconnect. Explicit account/program
  subscription options preserve the effective legacy binary, base58, base64, `jsonParsed` fallback, and
  `base64+zstd` response union without publishing no-op Agave fields.
- **Wire-level control.** Spec-accurate legacy, v0, and feature-gated SIMD-0385 V1 transaction building,
  signing, and decoding —
  money-critical encodings are checked against exact vectors from pinned Rust contracts, not only
  against C# round trips.
- **Complete signing workflows.** Typed Ed25519 and BLS12-381 values, local/external/null signers,
  partial signing, Rust-compatible key import/export, vote-account-bound BLS proofs of possession,
  PoP-gated same-message BLS aggregation,
  and domain-separated Solana off-chain messages.
- **Traceable parity.** Exact upstream commit pins, coverage boundaries, and exclusions are published
  in the [Rust parity matrix](https://github.com/jecacs/SolSharp/blob/v2.0.0/docs/RUST_PARITY.md); SolSharp
  is independently written and is not an official Anza/Solana product.
- **Purposeful dependencies.** A dependency-light Core, allocation-free hot paths and span-based APIs;
  the RPC resilience pipeline and vetted Ed25519/BLS backends are included deliberately.
- **Measured quality.** The unit suite covers 93.7% of hand-written production lines across the four
  functional assemblies. CI merges overlapping reports, excludes generated sources, publishes line and
  branch details, enforces a 90% repository-wide line floor, and rejects a stale documented percentage.
- **Automated security gates.** Pull requests and weekly scans audit direct and transitive NuGet
  advisories, review dependency changes, run CodeQL's extended C# security queries, and run OpenSSF
  supply-chain checks; release packages receive GitHub build-provenance attestations.

## Compared with Solnet and the official Rust contracts

Solnet is an established, ecosystem-oriented .NET SDK. This compact comparison uses its
[published 8.7.0 release](https://github.com/bmresearch/Solnet/commit/e8df87bdb2006376ba3eea9e1d3b857c84fc5685);
SolSharp is release 2.0.0; the reference column is the pinned
[official Rust parity matrix](https://github.com/jecacs/SolSharp/blob/v2.0.0/docs/RUST_PARITY.md).

| Capability | Official Rust SDK / Agave | SolSharp 2.0 | Solnet published 8.7.0 |
| --- | --- | --- | --- |
| **Transactions** | Legacy, V0, feature-gated SIMD-0385 V1 | Legacy/V0/V1 exact wire build, parse, signing, validation, and decompilation | Legacy/V0; the published decoder rejects versions above 0 |
| **RPC / PubSub** | 53 applicable request variants; nine subscription families and their effective config unions | 53/53 RPC; 9/9 PubSub, including exact HTTP/WS account-encoding unions, effective `SubscribeAccountWithOptionsAsync` / `SubscribeProgramWithOptionsAsync` configs, early signature events, and explicit V1 opt-ins | 50/53 RPC; 6/9 PubSub families |
| **Programs** | Canonical native and SPL crates | Deep native/SPL coverage, extensive Token-2022 interfaces, typed state/instruction decoders | Broader ecosystem program set; published package predates repository-head Token-2022 additions |
| **Offline signing** | Fixed slots, signer/presigner/null-signer, partial signing and verification | Typed fixed slots, partial/all signing, verified external signatures, `Presigner` / `NullSigner` | Partial signing and externally supplied signatures |
| **Deployment** | Native Rust crates | One package, generated JSON metadata, declared AOT compatibility, native-publish CI | Five modular packages; no published solution-wide AOT/trimming contract |
| **Provenance** | Authoritative source | Seven immutable upstream pins and byte-level KATs | No immutable upstream revision matrix in published documentation |

Solnet repository head contains newer unreleased work; in particular, its current class named V1 does not yet
use the pinned SIMD-0385 message body and message-first signature envelope. The full evidence-linked comparison
is in the [repository README](https://github.com/jecacs/SolSharp/blob/v2.0.0/README.md#how-it-compares-to-solnet).

## Quick start

```csharp
using SolSharp.Rpc;
using SolSharp.Wallet;
using SolSharp.Programs;

// DI with a built-in resilience pipeline (or: new SolanaRpcClient(httpClient))
services.AddSolanaRpc("https://your-rpc-endpoint");

var lamports = await rpc.GetBalanceAsync(account);

// Build, sign, and send a transfer
using var payer = Keypair.Parse(secret);
var blockhash = (await rpc.GetLatestBlockhashAsync()).Blockhash;

var tx = new TransactionBuilder()
    .SetRecentBlockhash(blockhash)
    .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1_000_000))
    .Build(payer);

var signature = await rpc.SendAndConfirmTransactionAsync(tx.Serialize());
```

```csharp
// WebSocket streaming
using SolSharp.Rpc;
using SolSharp.Rpc.Streaming;

await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://your-rpc-endpoint"));
await foreach (var slot in ws.SubscribeSlotsAsync())
    Console.WriteLine(slot.Slot);

var accountChanges = await ws.SubscribeAccountWithOptionsAsync(
    account,
    new AccountSubscriptionOptions { Encoding = RpcAccountEncoding.JsonParsed });
```

## Learn more

- [Usage guide](https://github.com/jecacs/SolSharp/blob/v2.0.0/docs/USAGE.md) — a task-oriented
  cookbook: keys, export, mnemonics, signed off-chain messages, reads, SPL token state, priority fees, v0 + address lookup
  tables, SIMD-0385 V1, durable nonces, decoding transactions, subscriptions, batching, and confirmation.
- [GitHub repository](https://github.com/jecacs/SolSharp)
- [Changelog](https://github.com/jecacs/SolSharp/blob/v2.0.0/CHANGELOG.md)
- [Upstream parity and provenance](https://github.com/jecacs/SolSharp/blob/v2.0.0/docs/RUST_PARITY.md)
- [Third-party notices](https://github.com/jecacs/SolSharp/blob/v2.0.0/THIRD_PARTY_NOTICES.md)

## Security

SolSharp handles private keys and builds transactions that move funds. It has **not** been audited —
use at your own risk. Never export a raw private key to an RPC provider, hosted service, or third-party
transaction builder: keep signing behind `ISigner`, inspect and simulate, then send only signed bytes.

A green security badge means the automated checks found no known issue at the tested revision. It is not
a guarantee that the package is vulnerability-free and does not replace an independent security audit.

BLS operations use the packaged native `blst` backend on `linux-x64`, `linux-arm64`, `osx-x64`,
`osx-arm64`, and `win-x64`. Other RIDs can use the rest of SolSharp, but cannot call its BLS API.

To report a vulnerability, use the
[security policy](https://github.com/jecacs/SolSharp/blob/main/SECURITY.md) — private reporting,
not a public issue.
