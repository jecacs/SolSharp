# SolSharp

A lean, modern .NET SDK for Solana: RPC + WebSocket streaming, wire-level transaction
signing/building. Optimised for low latency and a small dependency footprint — it is a
deliberate, focused alternative to the heavier general-purpose SDKs, not a clone of them.

Status: early. `SolSharp.Core` is being built first; `Wallet`, `Rpc`, `Programs` are planned.

## Commands

Run from the repo root (where `SolSharp.sln` lives):

- `dotnet build` — code style is enforced on build (`EnforceCodeStyleInBuild`), so style violations surface as warnings.
- `dotnet test` — NUnit suite.
- `dotnet format` — auto-applies the style. Note: it cannot auto-fix naming (IDE1006); fix those by hand.

## Hard rules

- **English only** — code, comments, identifiers, test names, docs.
- **Comments earn their place.** Explain *why* — non-obvious rationale, wire-format quirks, gotchas — never restate what the code already says. No filler, decorative, or obvious comments. XML docs on public API are welcome; inline noise is not.
- **Target framework is `net8.0`.** Do not use net9-only APIs (e.g. `JsonStringEnumMemberName`, `InlineArray`-based span tricks that need newer ref-safety).
- **Modern C# only.** File-scoped namespaces, `var`, collection expressions `[]`, primary constructors, switch expressions, pattern matching, `is null` / `is not null`. The full rule set lives in `.editorconfig` + `Directory.Build.props` — follow the analyzers, don't fight them. Do not restate style rules here.

## Architecture

Layering (dependencies point downward; no cycles):

- **Core** — byte-level types and codecs. No I/O, no crypto engine. Only dependency: `SimpleBase`.
- **Wallet** — the Ed25519 engine: sign, keygen, verify. Depends on Core.
- **Rpc** — HTTP JSON-RPC + WebSocket streaming client. Depends on Core.
- **Programs** — instruction builders + transaction building. Depends on Core.

Rules:

- `Core` references no other SolSharp project and pulls no network/crypto package. Litmus for "is it Core?": a pure type/constant/codec that everyone needs, with no I/O and no knowledge of a specific program/DEX.
- Folder = namespace.
- **Ed25519 / signing belongs in `Wallet`, never in `Core`.** Signature verification is exposed as an extension on `PublicKey` from `Wallet` (Core keeps the type, Wallet owns the crypto).

## Layout

```
SolSharp/
  src/SolSharp.Core/        Encoding/  Primitives/  Converters/  Constants/
  tests/SolSharp.Core.Tests/   mirrors src, nested test fixtures
```

## Testing

- NUnit + FluentAssertions + NSubstitute. NSubstitute only where there are real collaborators (pure utilities have nothing to mock).
- **One nested fixture per method under test:**
  `public static class XTests { [TestFixture] public sealed class Method { ... } }`.
- Wire formats and crypto are money-critical: cover them with known vectors (RFC 8032 for signing, canonical compact-u16 / base58 vectors), not just round-trips.
- `IDE1006` is disabled for `tests/**` so `Method_Scenario_Expectation` names are allowed.
- For constructor-throws-only tests use an explicit discard: `Action act = () => _ = new T(...);`.

## Security (money-critical)

- Anything that touches transaction bytes or signing must be tested against known-good vectors before it is trusted.
- Never commit secrets or private keys. `.gitignore` covers `*.key`, `.env`, `secrets.json`, `appsettings.*.local.json`.
- Never hand a raw private key to a third-party library. Build with theirs if needed, but sign with our own signer; simulate and assert instructions/amounts/destination before sending.

## Decisions

- `PublicKey` is a `readonly struct` backed by four `ulong` words (32 bytes inline, value equality, no per-key heap allocation). Base58 is cached only when the key is built from a string; from-bytes stays allocation-free. No zero-copy `AsSpan()` by design — use `CopyTo` / `ToBytes`.
- `Commitment` serializes via a custom `JsonConverter` applied as a `[JsonConverter]` attribute (net8 has no `JsonStringEnumMemberName`). The attribute makes it self-serializing under default options, not just `SolanaJsonSerializer.Options`.
- Wire enums/types follow that same pattern: self-serializing via attribute so they hold their wire form regardless of which `JsonSerializerOptions` are in play.
