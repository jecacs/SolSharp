# SolSharp

A lean, modern .NET SDK for Solana — RPC, WebSocket streaming, and wire-level
transaction signing and building.

SolSharp is built for low latency and a small dependency footprint. It is a focused,
hackable alternative to the heavier general-purpose SDKs: you get direct control over the
wire format and the signing path, without dragging in a large dependency graph. If you are
writing bots, indexers, or backend services that talk to Solana from .NET and care about
speed and control, this is aimed at you.

> **Status: early / pre-release.** `SolSharp.Core` and `SolSharp.Rpc` (HTTP reads + WebSocket
> streaming + DI) are in place; `Wallet` and `Programs` are planned. Nothing is on NuGet yet and
> the public API is not stable — expect breaking changes.

## Motivation

When this was started, the .NET options for Solana were either unmaintained and stale or
heavy and not built for performance — there was no modern, fast, actively-developed client.
SolSharp is a from-scratch answer to that: current C#, allocation-conscious, and tuned for
latency-sensitive workloads.

## Why

- **Lean.** No kitchen-sink dependency graph. `Core` depends on a single package (base58).
- **Wire-level control.** Hand-rolled, spec-accurate transaction encoding and Ed25519
  signing — the parts most SDKs hide — fully under your control and tested against known vectors.
- **Latency-minded.** Value types, allocation-free hot paths, span-based APIs.
- **Modern .NET.** C# latest, nullable reference types, code style enforced on build.

## Packages

| Package            | Purpose                                             | Status      |
| ------------------ | --------------------------------------------------- | ----------- |
| `SolSharp.Core`    | Primitives, encoding, JSON, program/sysvar constants | Usable      |
| `SolSharp.Rpc`     | HTTP JSON-RPC reads + WebSocket streaming + DI       | Usable      |
| `SolSharp.Wallet`  | Ed25519 keys, key parsing, raw transaction signing  | Planned     |
| `SolSharp.Programs`| Instruction builders + transaction building         | Planned     |

Dependencies point downward only: `Wallet`, `Rpc`, and `Programs` all build on `Core`,
which depends on nothing else in the solution and pulls no network or crypto package.

## What's here today

`SolSharp.Core`:

- `PublicKey` — a 32-byte value type with value equality, base58 parsing, and JSON support.
- `Base58` and `ShortVec` (compact-u16) — the encodings Solana uses on the wire.
- `Commitment` — an RPC enum that serializes to its exact wire string.
- `SolanaProgramIds`, `Sysvars`, `Mints` — well-known on-chain addresses, guarded by a test
  that every constant decodes to a valid 32-byte key.

```csharp
using SolSharp.Core.Primitives;

var mint = PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");
byte[] raw = mint.ToBytes();              // 32 bytes, allocation-free storage
bool ok = PublicKey.TryParse(input, out var key);
```

`SolSharp.Rpc`:

- HTTP JSON-RPC reads — `getBalance`, `getLatestBlockhash`, `getSlot`, `getBlockHeight`,
  `getTokenAccountBalance`, `getTokenSupply`, `getHealth`, `getVersion`, and more; each typed,
  fully documented, and tested.
- WebSocket streaming multiplexed over one connection: `SubscribeSlotsAsync` (`IAsyncEnumerable`)
  and `SubscribeLogsAsync` (`ChannelReader`).
- DI registration with a built-in resilience pipeline (retry on transient errors and HTTP 429).

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

## Roadmap

- [x] Core primitives — `PublicKey`, `Base58`, `ShortVec`
- [x] RPC enum + JSON converters (`Commitment`)
- [x] Program / sysvar / mint constants + validation
- [ ] `SolSharp.Wallet` — Ed25519 signing, key parsing, raw transaction signer
- [x] `SolSharp.Rpc` — HTTP JSON-RPC reads, WebSocket streaming (multiplexed), DI + resilience
- [ ] `SolSharp.Rpc` next — `getAccountInfo` / `getSignaturesForAddress`, `accountSubscribe`, auto-reconnect
- [ ] `SolSharp.Programs` — System / Token / ATA / Compute Budget instructions, transaction builder
- [ ] Published NuGet packages

## Requirements

- .NET 8 SDK or later.

## Build & test

```bash
dotnet build
dotnet test
dotnet format   # apply the enforced code style
```

## Layout

```
SolSharp/
  src/SolSharp.Core/   Encoding/  Primitives/  Converters/  Constants/
  src/SolSharp.Rpc/    Protocol/  Models/  Streaming/  + client, options, DI
  tests/               NUnit + FluentAssertions, mirroring each project
  .editorconfig        modern C# style, enforced on build
  Directory.Build.props
  CLAUDE.md            conventions and decisions for contributors/agents
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

[MIT](LICENSE) © yevhen
