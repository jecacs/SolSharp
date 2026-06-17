# SolSharp

A lean, modern .NET SDK for Solana: RPC + WebSocket streaming, wire-level transaction
signing/building. Optimised for low latency and a small dependency footprint — it is a
deliberate, focused alternative to the heavier general-purpose SDKs, not a clone of them.

Status: early. All four projects are in place: Core primitives (incl. a Borsh reader/writer), the Rpc client (reads + typed SPL account state via `Mint`/`TokenAccount`, send/simulate, typed transaction errors, multiplexed WebSocket streaming with auto-reconnect, DI + resilience), the Wallet (Ed25519 keys, signing, verification, key parsing), and Programs (System/Token/ATA/Compute Budget/Memo + the Address Lookup Table program, PDA/ATA, legacy + v0 transaction building/signing/parsing, and instruction decompilation). A separate live integration suite exercises the read and streaming paths against a real cluster.

## Commands

Run from the repo root (where `SolSharp.sln` lives):

- `dotnet build` — code style is enforced on build (`EnforceCodeStyleInBuild`), so style violations surface as warnings.
- `dotnet test` — NUnit suite.
- `dotnet format` — auto-applies the style. Note: it cannot auto-fix naming (IDE1006); fix those by hand.

## Hard rules

- **English only** — code, comments, identifiers, test names, docs.
- **Comments earn their place.** Explain *why* — non-obvious rationale, wire-format quirks, gotchas — never restate what the code already says. No filler, decorative, or obvious comments. Public API carries full XML docs (summary, every `<param>`, `<returns>`, and thrown `<exception>`); inline noise does not.
- **Attributes on their own line** — never inline with the member, e.g. `[JsonPropertyName("id")]` goes above the property, not beside it. `dotnet format` does not enforce this (only Rider does), so write it that way by hand.
- **Target framework is `net8.0`.** Do not use net9-only APIs (e.g. `JsonStringEnumMemberName`, `InlineArray`-based span tricks that need newer ref-safety).
- **Modern C# only.** File-scoped namespaces, `var`, collection expressions `[]`, primary constructors, switch expressions, pattern matching, `is null` / `is not null`. The full rule set lives in `.editorconfig` + `Directory.Build.props` — follow the analyzers, don't fight them. Do not restate style rules here.

## Architecture

Layering (dependencies point downward; no cycles):

- **Core** — byte-level types and codecs. No I/O, no crypto engine. Only dependency: `SimpleBase`.
- **Wallet** — the Ed25519 engine: sign, keygen, verify. Depends on Core.
- **Rpc** — HTTP JSON-RPC + WebSocket streaming client. Depends on Core.
- **Programs** — instruction builders, PDA/ATA derivation, message compilation, transaction building. Depends on Core and Wallet (for `ISigner` and the on-curve check).

Rules:

- `Core` references no other SolSharp project and pulls no network/crypto package. Litmus for "is it Core?": a pure type/constant/codec that everyone needs, with no I/O and no knowledge of a specific program/DEX.
- Folder = namespace.
- **Ed25519 / signing belongs in `Wallet`, never in `Core`.** Signature verification is exposed as an extension on `PublicKey` from `Wallet` (Core keeps the type, Wallet owns the crypto).

## Layout

```
SolSharp/
  src/SolSharp.Core/        Encoding/  Primitives/  Converters/  Constants/
  src/SolSharp.Rpc/         Protocol/  Models/  Streaming/  + client, options, DI
  src/SolSharp.Wallet/      Keypair (+ parsing), ISigner, PublicKeyExtensions, Ed25519Curve
  src/SolSharp.Programs/    AccountMeta/Instruction, Message + MessageV0, Transaction, TransactionBuilder, program builders (System/Token/ATA/Compute Budget/Memo/ALT), PDA/ATA
  tests/                    SolSharp.{Core,Rpc,Wallet,Programs}.Tests (nested fixtures, mirroring src) + SolSharp.IntegrationTests (live cluster)
```

## Testing

- NUnit + FluentAssertions + NSubstitute. NSubstitute only where there are real collaborators (pure utilities have nothing to mock).
- **Every public member is done only when it has both full XML docs and a test.** Don't skip a test because the method resembles one already covered — cover each distinct response/parse shape and each request param shape.
- **One nested fixture per method under test:**
  `public static class XTests { [TestFixture] public sealed class Method { ... } }`.
- Wire formats and crypto are money-critical: cover them with known vectors (RFC 8032 for signing, canonical compact-u16 / base58 vectors), not just round-trips.
- `IDE1006` is disabled for `tests/**` so `Method_Scenario_Expectation` names are allowed.
- For constructor-throws-only tests use an explicit discard: `Action act = () => _ = new T(...);`.
- **Integration tests** live in `SolSharp.IntegrationTests`, hit a real cluster, and run as part of `dotnet test`. They are tagged `[Category("Integration")]`; the endpoint defaults to public mainnet and is overridable via the `SOLSHARP_RPC_URL` / `SOLSHARP_WS_URL` environment variables (no key is ever committed). They report inconclusive — not failed — on rate limits or transport errors, so a busy node never reddens the suite. Skip them for a fast offline run with `dotnet test --filter "TestCategory!=Integration"`.

## Security (money-critical)

- Anything that touches transaction bytes or signing must be tested against known-good vectors before it is trusted.
- Never commit secrets or private keys. `.gitignore` covers `*.key`, `.env`, `secrets.json`, `appsettings.*.local.json`.
- Never hand a raw private key to a third-party library. Build with theirs if needed, but sign with our own signer; simulate and assert instructions/amounts/destination before sending.

## Decisions

- `PublicKey` is a `readonly struct` backed by four `ulong` words (32 bytes inline, value equality, no per-key heap allocation). Base58 is cached only when the key is built from a string; from-bytes stays allocation-free. No zero-copy `AsSpan()` by design — use `CopyTo` / `ToBytes`.
- `Commitment` serializes via a custom `JsonConverter` applied as a `[JsonConverter]` attribute (net8 has no `JsonStringEnumMemberName`). The attribute makes it self-serializing under default options, not just `SolanaJsonSerializer.Options`.
- Wire enums/types follow that same pattern: self-serializing via attribute so they hold their wire form regardless of which `JsonSerializerOptions` are in play.
- **Ed25519 lives in `Wallet` on `BouncyCastle.Cryptography`** — not the .NET BCL (net8/10 ship no usable cross-platform `Ed25519`: Windows unsupported, Apple's is non-conformant) and not a hand-rolled curve. Pure-managed/portable was chosen over libsodium/NSec's native dependency, since signing throughput is not the bottleneck; `ISigner` keeps the backend swappable.
- `Keypair` is one word to match the Solana ecosystem (`solana-keygen`, web3.js `Keypair`), not .NET's `KeyPair`. It stores only the 32-byte seed, derives the public key once, and zeroes the seed on `Dispose`.
- Transactions support both the **legacy** and **v0 (versioned)** message formats behind a shared `ITransactionMessage`, so `Transaction` signs and serializes either. Account ordering matches Solana's compilation exactly (fee payer first, then accounts sorted by public-key bytes within the writable-signer / readonly-signer / writable / readonly classes). v0 additionally drains non-signer, non-program accounts found in a supplied lookup table into a table lookup and prefixes the `0x80` version byte. Both are validated byte-for-byte against `solana-sdk` (solders).
- `PublicKey.IsOnCurve` is direct field arithmetic, not BouncyCastle: BC's public-key validation rejects non-canonical encodings (y ≥ p) that Solana's `curve25519-dalek` accepts after reducing mod p. It is fuzzed against solders so PDA/ATA derivation matches the network.
- **SPL Token account state uses the fixed-size `Pack` layout, not Borsh.** `Mint` (82 bytes) and `TokenAccount` (165 bytes) read a `COption` as a 4-byte little-endian tag followed by an *always-present* value (the slot is reserved even when `None`) — unlike Borsh's 1-byte tag with the value present only when `Some`. So `BorshReader` / `BorshWriter` are for Anchor/Borsh data; the SPL decoders are hand-written against the Pack layout and KAT'd against `solders.token.state`. (The Token *instruction* data is different again: a minimal `COption` of a 1-byte tag plus the value only when `Some`.)
- Money-critical encodings (message/transaction serialization, instruction data, PDA/ATA, on-curve) are checked byte-for-byte against `solana-sdk` (solders) and `solana-py`, not just round-trips.
