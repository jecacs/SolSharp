# SolSharp

A modern, contract-driven .NET SDK for Solana: keys and signing, program instructions,
transaction wire formats, RPC, and WebSocket streaming. It is optimized for low latency,
bounded hostile-input handling, focused dependencies with a dependency-light Core, and Native AOT.

Status: 2.0.0. SolSharp is independently implemented
against immutable Anza Solana SDK, Agave, and SPL source revisions; exact pins, client-side
coverage, verification criteria, and deliberate node/runtime exclusions live in
`docs/RUST_PARITY.md`. All JSON used by the library is source-generated and all four functional
assemblies are Native AOT compatible; the package also contains a minimal facade. The live integration suite exercises read, streaming,
and devnet write paths against real nodes.

## Commands

Run from the repo root (where `SolSharp.sln` lives):

- `dotnet build` — Roslyn and StyleCop code style is enforced on build (`EnforceCodeStyleInBuild`), so actionable style violations surface as warnings. Repository-specific StyleCop suppressions and the shared Rider/Roslyn modifier order live in `.editorconfig`.
- `dotnet test` — NUnit suite.
- `dotnet format` — auto-applies the style. Note: it cannot auto-fix naming (IDE1006); fix those by hand.

## Hard rules

- **Never use `ConfigureAwait`.** Do not write `.ConfigureAwait(false)` or `.ConfigureAwait(true)` anywhere — not in library code, not in tests. This is a deliberate, permanent project choice; do not suggest adding it.
- **English only** — code, comments, identifiers, test names, docs.
- **Comments earn their place.** Explain *why* — non-obvious rationale, wire-format quirks, gotchas — never restate what the code already says. No filler, decorative, or obvious comments. Public API carries full XML docs (summary, every `<param>`, `<returns>`, and thrown `<exception>`); inline noise does not.
- **Default to `internal`; `public` is a deliberate contract.** This is a library, so the public surface is an API others depend on — keep it minimal. A type is `public` only when a consumer constructs, receives, or catches it (i.e. it appears in a public signature). Everything else — request/response plumbing, converters, sinks, internal helpers — is `internal`, and tests reach it through `InternalsVisibleTo`.
- **Attributes on their own line** — never inline with the member, e.g. `[JsonPropertyName("id")]` goes above the property, not beside it. `dotnet format` does not enforce this (only Rider does), so write it that way by hand.
- **Target framework is `net8.0`.** Do not use net9-only APIs (e.g. `JsonStringEnumMemberName`,
  `InlineArray`-based span tricks that need newer ref-safety). `global.json` starts at SDK 8.0.100
  with `rollForward: major`, so a development machine with only a newer SDK can still build the
  repository. Hosted runners also contain newer SDKs; after installing 8.x, every CI/release job asserts
  the resolver's actual `dotnet --version` is 8.x. Do not remove that check or CI could silently stop
  proving the minimum if SDK selection or runner contents change.
- **Modern C# 12 only.** File-scoped namespaces, `var`, collection expressions `[]`, primary constructors, switch expressions, pattern matching, `is null` / `is not null`. The full rule set lives in `.editorconfig` + `Directory.Build.props` — follow the analyzers, don't fight them. Do not restate style rules here.
- **A feature is not done until it is documented.** Every user-facing addition or change lands in the same commit with all four documentation layers: (1) XML docs on the public API (CS1591 enforces presence on production members; full `<param>`, `<returns>`, and `<exception>` content remains a review policy); (2) `docs/USAGE.md` — a runnable example in the matching section (or a new section + `Contents` entry), with every snippet checked against the real signatures and model properties, not written from memory; (3) `README.md` — the wire-method list, feature bullets, and Layout if the shape of the repo changed (`README.nuget.md` only if the pitch/quick-start changes — it carries no method lists by design); (4) `CHANGELOG.md` under the release being prepared. Release-only extras: bump `Version` in `Directory.Build.props`, refresh `PackageReleaseNotes` in `src/SolSharp/SolSharp.csproj` (nuget.org shows only the current version's notes), and update the `Status:` line here.

## Architecture

Layering (dependencies point downward; no cycles):

- **Core** — byte-level types and codecs. No I/O, no crypto engine. Only dependency: `SimpleBase`.
- **Wallet** — Ed25519 and BLS12-381 key/signature engines plus offline signing contracts. Depends on Core.
- **Rpc** — HTTP JSON-RPC + WebSocket streaming client. Depends on Core.
- **Programs** — instruction builders, PDA/ATA derivation, message compilation, transaction building. Depends on Core and Wallet (for `ISigner` and the on-curve check).

Rules:

- `Core` references no other SolSharp project and pulls no network/crypto package. Litmus for "is it Core?": a pure type/constant/codec that everyone needs, with no I/O and no knowledge of a specific program/DEX.
- Folder = namespace.
- **Cryptographic key/signature engines belong in `Wallet`, never in `Core`.** Ed25519 verification is exposed as an extension on `PublicKey` from Wallet; the BLS value types and native backend also stay there.

## Layout

```
SolSharp/
  src/SolSharp.Core/        Encoding/  Primitives/  Converters/  Constants/  SysvarStates/
  src/SolSharp.Rpc/         Protocol/  Models/  Streaming/  + client, options, DI
  src/SolSharp.Wallet/      Ed25519/BLS keys, signers, verification, off-chain messages
  src/SolSharp.Programs/    native/SPL clients and states, legacy/v0/V1 messages and transactions, PDA/ATA
  src/SolSharp/             packaging facade: bundles the four assemblies into the single SolSharp NuGet package (no source of its own)
  tests/                    SolSharp.{Core,Rpc,Wallet,Programs}.Tests (nested fixtures, mirroring src) + SolSharp.IntegrationTests (live cluster)
  benchmarks/               SolSharp.Benchmarks: a standalone BenchmarkDotNet harness, outside the solution (run with dotnet run -c Release --project benchmarks/SolSharp.Benchmarks)
  samples/                  SolSharp.AotSmoke: the Native AOT smoke sample, part of the solution (so regular builds compile it); CI packs SolSharp, consumes that nupkg, publishes with PublishAot, and runs the binary
  docs/                     USAGE.md task guide and RUST_PARITY.md pinned compatibility matrix
  THIRD_PARTY_NOTICES.md    compatibility-source pins and native dependency attribution
```

## Testing

- NUnit + FluentAssertions + NSubstitute. NSubstitute only where there are real collaborators (pure utilities have nothing to mock).
- **Every public member is done only when it has both full XML docs and a test.** Don't skip a test because the method resembles one already covered — cover each distinct response/parse shape and each request param shape.
- **One nested fixture per method under test:**
  `public static class XTests { [TestFixture] public sealed class Method { ... } }`.
- Wire formats and crypto are money-critical: cover them with known vectors (RFC 8032 for signing, canonical compact-u16 / base58 vectors), not just round-trips.
- `IDE1006` is disabled for `tests/**` so `Method_Scenario_Expectation` names are allowed.
- For constructor-throws-only tests use an explicit discard: `Action act = () => _ = new T(...);`.
- **Arrange / Act / Assert comments.** Mark the three phases with `// Arrange`, `// Act`, `// Assert`. When the call under test and its check are a single fluent statement (exception delegates, `(await …).Should()…`), use one `// Act & Assert`. Skip the labels on expression-bodied or single-statement `[TestCase]` tests where there is nothing to separate — never restructure a test body just to fit them.
- **Integration tests** live in `SolSharp.IntegrationTests`, hit a real cluster, and run as part of `dotnet test`. They are tagged `[Category("Integration")]`; read/streaming tests default to public mainnet (`SOLSHARP_RPC_URL` / `SOLSHARP_WS_URL` override), and the write suite (airdrop, transfer, durable nonce) always targets devnet (`SOLSHARP_DEVNET_RPC_URL` override) — never mainnet. No key is ever committed. They report inconclusive — not failed — on rate limits or transport errors, so a busy node never reddens the suite. Skip them for a fast offline run with `dotnet test --filter "TestCategory!=Integration"`.

## Security (money-critical)

- Anything that touches transaction bytes or signing must be tested against known-good vectors before it is trusted.
- Never commit secrets or private keys. `.gitignore` covers `*.key`, `.env`, `secrets.json`, `appsettings.*.local.json`.
- Never expose or export a raw private key to an RPC provider, hosted service, or third-party transaction builder. Keep signing behind `ISigner`; cryptographic backends are vetted implementation dependencies, not key-custody integrations. Simulate and assert instructions/amounts/destination before sending.

## Decisions

- `PublicKey` is a `readonly struct` backed by four `ulong` words (32 bytes inline, value equality, no per-key heap allocation). Base58 is cached only when the key is built from a string; from-bytes stays allocation-free. No zero-copy `AsSpan()` by design — use `CopyTo` / `ToBytes`.
- `Commitment` serializes via a custom `JsonConverter` applied as a `[JsonConverter]` attribute (net8 has no `JsonStringEnumMemberName`). The attribute makes it self-serializing under default options, not just `SolanaJsonSerializer.Options`.
- Wire enums/types follow that same pattern: self-serializing via attribute so they hold their wire form regardless of which `JsonSerializerOptions` are in play.
- **JSON is source-generated; reflection serialization is banned in src.** All RPC/WS paths go through the internal `RpcJson.Options` (resolver: `JsonTypeInfoResolver.Combine(SolanaJsonContext, CoreJsonContext)`; `SolanaJsonContext` in `Rpc/Protocol/` holds the closed root registrations); Core's public `SolanaJsonSerializer.Options` covers only the Core primitives via the public `CoreJsonContext`, with no reflection fallback. GOTCHA: a source-gen context can only materialize a converter-attributed type if it can construct the converter - an inaccessible converter makes the generator drop the type (SYSLIB1220 + SYSLIB1030; warnings locally, errors under CI's `-warnaserror`) and every use fails at runtime with `NotSupportedException`. That is why converter-attributed Core wire converters such as `CommitmentJsonConverter`, `PublicKeyJsonConverter`, and `HashJsonConverter` are **public**: keep these converters public, and keep `CoreJsonContext` in the chain. Consequences: request `params` entries are object-typed and dispatch by **exact runtime type**, so every boxed shape (configs in `Protocol/RpcParams.cs`, primitives, arrays — collections are pinned with `ToArray()`) must be registered in the context; anonymous types cannot be used in requests; a new `SendAsync<T>`/subscription/batch root type must be added to `SolanaJsonContext` (unregistered types throw `NotSupportedException`, which the offline client tests catch); types behind hand-written converters are invisible to the generator's graph walk, so what a converter reads via `options.GetTypeInfo<T>()` needs explicit registration. Every production project sets `IsAotCompatible` — the trim/AOT analyzers plus `-warnaserror` reject `RequiresUnreferencedCode`/`RequiresDynamicCode` APIs (e.g. `JsonSerializer.Serialize(..., options)` overloads, `ValidateDataAnnotations`). CI consumes the generated nupkg for its native smoke test, so the packaging layer is covered too.
- **Ed25519 lives in `Wallet` on `BouncyCastle.Cryptography`** — not the .NET BCL (net8/10 ship no usable cross-platform `Ed25519`: Windows unsupported, Apple's is non-conformant) and not a hand-rolled curve. Pure-managed/portable was chosen over libsodium/NSec's native dependency, since signing throughput is not the bottleneck; `ISigner` keeps the backend swappable.
- **BLS12-381 lives in `Wallet` on `Nethermind.Crypto.Bls` 1.0.5 / Supranational `blst`** — the pinned Solana min-pk POP ciphersuite is implemented over a vetted native backend, never hand-rolled. Parse and verify paths require canonical subgroup points and reject infinity; secrets are canonical little-endian scalars and are zeroed. Supported packaged RIDs are Linux x64/arm64, macOS x64/arm64, and Windows x64; keep the facade dependency and `THIRD_PARTY_NOTICES.md` in sync.
- `Keypair` is one word to match the Solana ecosystem (`solana-keygen`, web3.js `Keypair`), not .NET's `KeyPair`. It stores only the 32-byte seed, derives the public key once, and zeroes the seed on `Dispose`.
- Solana version-0 signed off-chain messages live in `Wallet`: they use the pinned SDK's exact
  `0xffsolana offchain` domain, canonical bounded ASCII/UTF-8 formats, SHA-256 hashing, and the same strict
  Ed25519 signer/verification path. They are not transactions and examples must not imply on-chain authority.
- Transactions support **legacy**, **v0**, and feature-gated **SIMD-0385 V1** messages behind `ITransactionMessage`. Account ordering matches the pinned Solana SDK (fee payer first, then public-key byte order within writable-signer / readonly-signer / writable / readonly classes). v0 drains eligible accounts into lookup tables and prefixes `0x80`; V1 prefixes `0x81`, keeps all addresses inline, carries an inline execution config, and writes the message before its fixed number of signatures. V1's omitted compute/data limits mean zero and cluster activation is external, so examples must set deliberate limits and never imply universal availability. All formats use exact pinned Rust vectors.
- `PublicKey.IsOnCurve` is direct field arithmetic, not BouncyCastle: BC's public-key validation rejects non-canonical encodings (y ≥ p) that Solana's `curve25519-dalek` accepts after reducing mod p. It is fuzzed against solders so PDA/ATA derivation matches the network.
- **SPL Token account state uses the fixed-size `Pack` layout, not Borsh.** `Mint` (82 bytes) and `TokenAccount` (165 bytes) read a `COption` as a 4-byte little-endian tag followed by an *always-present* value (the slot is reserved even when `None`) — unlike Borsh's 1-byte tag with the value present only when `Some`. So `BorshReader` / `BorshWriter` are for Anchor/Borsh data; the SPL decoders are hand-written against the Pack layout and KAT'd against `solders.token.state`. (The Token *instruction* data is different again: a minimal `COption` of a 1-byte tag plus the value only when `Some`.)
- Money-critical encodings (message/transaction serialization, instruction data, PDA/ATA, on-curve) are checked byte-for-byte against `solana-sdk` (solders) and `solana-py`, not just round-trips.
- **Ships as one NuGet package.** The source stays four layered projects (so the compiler keeps Core crypto/IO-free, Wallet owns Ed25519, etc.), but only the `src/SolSharp` facade is packable: it references the four with `PrivateAssets="all"` and an MSBuild target (`BundleProjectReferences`) folds their DLLs + XML docs into a single `SolSharp` package, re-declaring the real third-party deps (kept in sync by hand). PDBs are embedded (`DebugType=embedded`) so symbols ride inside the bundled DLLs rather than a near-empty `.snupkg`. Default-`false` `IsPackable` (overridden only for `MSBuildProjectName == SolSharp`) keeps every other project from emitting its own package. Package validation compares the packed public API with the previous stable `PackageValidationBaselineVersion`; update that baseline deliberately when preparing each release.
