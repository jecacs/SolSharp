# SolSharp

A lean, modern, Native AOT-ready .NET SDK for Solana — RPC, WebSocket streaming, and
wire-level transaction signing and building. No reflection anywhere: all JSON is
source-generated, and every assembly compiles clean to a native binary.

SolSharp is built for low latency and a small dependency footprint. If you are writing
bots, indexers, or backend services that talk to Solana from .NET and care about speed
and control, this is aimed at you.

## Why SolSharp

- **Native AOT ready.** Source-generated JSON (no reflection), trimmable, AOT-clean — ship a
  self-contained native binary with instant startup. CI runs a native-compiled smoke test on every push.
- **Full RPC coverage.** The complete current JSON-RPC HTTP read surface, send/simulate,
  batching, and multiplexed WebSocket subscriptions with automatic reconnect.
- **Wire-level control.** Spec-accurate legacy and v0 transaction building, signing, and decoding —
  every encoding checked byte-for-byte against the Rust `solana-sdk`.
- **Lean.** No kitchen-sink dependency graph; allocation-free hot paths and span-based APIs.

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
await using var ws = new SolanaWsClient();
await ws.ConnectAsync(new Uri("wss://your-rpc-endpoint"));
await foreach (var slot in ws.SubscribeSlotsAsync())
    Console.WriteLine(slot.Slot);
```

## Learn more

- [Usage guide](https://github.com/jecacs/SolSharp/blob/main/docs/USAGE.md) — a task-oriented
  cookbook: keys and mnemonic import, reads, SPL token state, priority fees, v0 + address lookup
  tables, durable nonces, decoding transactions, subscriptions, batching, and confirmation.
- [GitHub repository](https://github.com/jecacs/SolSharp)
- [Changelog](https://github.com/jecacs/SolSharp/blob/main/CHANGELOG.md)

## Security

SolSharp handles private keys and builds transactions that move funds. It has **not** been audited —
use at your own risk. Never hand a raw private key to a dependency you do not control: sign with your
own signer and simulate before sending.
