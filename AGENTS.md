# SolSharp

A modern, contract-driven .NET SDK for Solana: keys and signing, program instructions,
transaction wire formats, RPC, and WebSocket streaming. It is optimized for low latency,
bounded hostile-input handling, focused dependencies with a dependency-light Core, and Native AOT.

Status: 3.2.0. SolSharp is independently implemented
against immutable Anza Solana SDK, Agave, and SPL source revisions; exact pins, client-side
coverage, verification criteria, and deliberate node/runtime exclusions live in
`docs/RUST_PARITY.md`. All JSON used by the library is source-generated and all four functional
assemblies are Native AOT compatible; the package also contains a minimal facade. The live integration suite exercises read, streaming,
and devnet write paths against real nodes.

## Commands

Run from the repo root (where `SolSharp.sln` lives):

- `dotnet build` — Roslyn and StyleCop code style is enforced on build (`EnforceCodeStyleInBuild`), so actionable style violations surface as warnings. Repository-specific StyleCop severities live in `.editorconfig`; the member-order precedence is explicit in `stylecop.json`.
- `dotnet test` — NUnit suite. Add `--filter "TestCategory!=Integration"` for a fast offline run.
- `dotnet format` — applies supported style fixes. It cannot reorder members (SA1201/SA1202/SA1203/SA1204/SA1214) or fix naming (IDE1006); move or rename those by hand.

**Before pushing, run the core developer gates with the same flags CI uses.** `.githooks/pre-push` runs
locked restores, Release builds, and analyzer-format checks for the solution and benchmark project, plus
the offline solution tests. CI additionally enforces coverage, documentation consistency, package/API
validation, and the Native AOT consumer smoke. Enable the hook once per clone with
`git config core.hooksPath .githooks` (Rider runs Git hooks on push).
Bypass with `git push --no-verify`, `SOLSHARP_SKIP_PREPUSH=1`, or `SOLSHARP_PREPUSH_SKIP_TESTS=1`.

```
dotnet build --no-restore --configuration Release -warnaserror
dotnet format --no-restore --verify-no-changes --severity info
dotnet build benchmarks/SolSharp.Benchmarks/SolSharp.Benchmarks.csproj --no-restore --configuration Release -warnaserror
dotnet format benchmarks/SolSharp.Benchmarks/SolSharp.Benchmarks.csproj --no-restore --verify-no-changes --severity warn
```

Three traps worth knowing:

- **`--severity info` is the gate for the solution**, not the default `warn`. Info-level analyzers such as
  CA1859 (`Change type of parameter … for improved performance`) fail CI and are invisible to a bare
  `dotnet format --verify-no-changes`.
- **Benchmarks are checked at `--severity warn`, deliberately** — their methods stay instance members and
  cold-start cases build fresh options, so info-level advice does not apply there. They are also outside
  the solution, so they need their own build/format/restore invocations.
- **A local incremental build hides warnings.** MSBuild skips analyzers for projects it considers
  up-to-date, so pre-existing violations in untouched files never reappear. CI always builds a clean
  checkout; locally add `--no-incremental` when you want the same picture.

## Hard rules

- **Never use `ConfigureAwait`.** Do not write `.ConfigureAwait(false)` or `.ConfigureAwait(true)` anywhere — not in library code, not in tests. This is a deliberate, permanent project choice; do not suggest adding it.
- **English only** — code, comments, identifiers, test names, docs.
- **Comments earn their place.** Explain *why* — non-obvious rationale, wire-format quirks, gotchas — never restate what the code already says. No filler, decorative, or obvious comments. Public API carries full XML docs (summary, every `<param>`, `<returns>`, and thrown `<exception>`); inline noise does not.
  **Default to none.** An inline comment is the exception, not the accompaniment: write it only when a
  competent reader would otherwise get it *wrong*, not merely find it *slow*. Concretely, an inline comment
  earns its place only if it does one of these:
  a magic constant (`d = -121665/121666 mod p`), a rule imposed by an external wire format or upstream
  implementation, a deliberate deviation someone would otherwise "fix", or a hazard that is invisible at
  the call site. Everything else is noise.
  Three habits to avoid, in rough order of how often they show up:
  **narrating the next line** (`// A carry out of the top limb means the value is too large` above
  `if (carry != 0)`); **putting the commit message in the code** — the history of *why this changed*,
  benchmark numbers and before/after comparisons belong in `CHANGELOG.md` and the commit, while the code
  says only what is true now; and **restating the XML doc** a few lines below it.
  One line beats four. If the explanation needs a paragraph, the code usually needs a better name or a
  smaller method instead.
- **Default to `internal`; `public` is a deliberate contract.** This is a library, so the public surface is an API others depend on — keep it minimal. A type is `public` only when a consumer constructs, receives, or catches it (i.e. it appears in a public signature). Everything else — request/response plumbing, converters, sinks, internal helpers — is `internal`, and tests reach it through `InternalsVisibleTo`.
- **Attributes on their own line** — never inline with the member, e.g. `[JsonPropertyName("id")]` goes above the property, not beside it. `dotnet format` does not enforce this (only Rider does), so write it that way by hand.
- **Member order is build-gated.** Group fields, constructors, finalizers, delegates, events, enums, interfaces, properties, indexers, conversions/operators, methods, and nested types in that order. Within each kind use `public`, `internal`, `protected internal`, `protected`, `private protected`, then `private`; therefore a private method never splits public methods. The remaining precedence is `const`, `static`, then `readonly`, as declared in `stylecop.json` and enforced by SA1201/SA1202/SA1203/SA1204/SA1214.
- **Target framework is `net10.0`.** Do not use APIs that require a later target framework.
  **`global.json` pins one exact SDK with `rollForward: disable`; every CI/release job that restores or
  builds the checkout installs it via `global-json-file: global.json`.** The pin is not stylistic: `IsAotCompatible` injects
  `Microsoft.NET.ILLink.Tasks` and `PublishAot` injects `Microsoft.DotNet.ILCompiler`, both versioned by
  the SDK's *runtime* (10.0.303 carries runtime 10.0.11), and both are recorded in every
  `packages.lock.json` as direct references. A floating SDK therefore breaks
  `dotnet restore --locked-mode` with NU1004 the moment .NET ships a patch, with no source change at all.
  Every checkout job that restores or builds asserts `dotnet --version` equals the pinned value; do not
  weaken that back to a `10.*` prefix test, or a mismatched SDK will surface as a confusing lock error
  instead of a clear one. The checkout-free `publish_package` job is the deliberate exception: it neither
  restores nor builds and pins its independently installed 10.0.x SDK in an isolated temporary
  `global.json` before it verifies and publishes the staged artifacts.
  **Bumping the SDK is a deliberate, four-part change:** update `global.json`, regenerate the solution
  locks (`dotnet restore --force-evaluate`), regenerate `benchmarks/` separately because it is outside the
  solution, and hand-update the ILLink/ILCompiler entries in
  `samples/SolSharp.AotSmoke/packages.packed.lock.template.json`. The same four steps apply to adding any
  `PackageReference` in `Directory.Build.props`.
- **Modern C# 14 only.** `LangVersion=latest` means the latest stable language supported by the pinned .NET 10 SDK line; preview syntax is not enabled. File-scoped namespaces, `var`, collection expressions `[]`, primary constructors, switch expressions, pattern matching, `is null` / `is not null`. The full rule set lives in `.editorconfig`, `stylecop.json`, and `Directory.Build.props` — follow the analyzers, don't fight them.
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
  CONTRIBUTING.md           contributor process; defers to this file for layering and hard rules
  CODE_OF_CONDUCT.md        community expectations
  SECURITY.md               disclosure policy and the automated security gates
  CHANGELOG.md              Keep-a-Changelog history; every user-visible change lands here
  BannedSymbols.txt         APIs banned at build time via RS0030 (see the ConfigureAwait hard rule)
  .github/                  CI/release/security workflows, issue and pull-request templates
```

## Testing

- NUnit + FluentAssertions + NSubstitute. NSubstitute only where there are real collaborators (pure utilities have nothing to mock).
- **Every public member is done only when it has both full XML docs and a test.** Don't skip a test because the method resembles one already covered — cover each distinct response/parse shape and each request param shape.
- **One nested fixture per method under test:**
  `public static class XTests { [TestFixture] public sealed class Method { ... } }`.
- Wire formats and crypto are money-critical: cover them with known vectors (RFC 8032 for signing, canonical compact-u16 / base58 vectors), not just round-trips.
- Property-based hostile-input tests use FsCheck with an explicit replay seed and bounded case counts/input
  sizes (`MaxTest`, plus `EndSize` where collections are generated). They complement upstream vectors; they
  never replace them or introduce nondeterministic CI failures.
- `IDE1006` is disabled for `tests/**` so `Method_Scenario_Expectation` names are allowed.
- For constructor-throws-only tests use an explicit discard: `Action act = () => _ = new T(...);`.
- **Arrange / Act / Assert comments.** Mark the three phases with `// Arrange`, `// Act`, `// Assert`. When the call under test and its check are a single fluent statement (exception delegates, `(await …).Should()…`), use one `// Act & Assert`. Skip the labels on expression-bodied or single-statement `[TestCase]` tests where there is nothing to separate — never restructure a test body just to fit them.
- **Integration tests** live in `SolSharp.IntegrationTests`, hit a real cluster, and run as part of `dotnet test`. They are tagged `[Category("Integration")]`; read/streaming tests default to public mainnet (`SOLSHARP_RPC_URL` / `SOLSHARP_WS_URL` override), and the write suite (airdrop, transfer, durable nonce) always targets devnet (`SOLSHARP_DEVNET_RPC_URL` override) — never mainnet. HTTP read and write harnesses use shared two-request-per-second token buckets; WebSocket probes are serialized and their starts are paced at 500 ms. Write fixtures additionally carry `[Category("DevnetWrite")]` and are non-parallel. No key is ever committed. Ordinary runs report transient endpoint/faucet failures as inconclusive. The release gate is strict for unit/read/streaming tests, while it attempts the faucet-dependent write probe separately so a shared-faucet 429 cannot block publication; deterministic write-path failures still fail. Skip all live tests for a fast offline run with `dotnet test --filter "TestCategory!=Integration"`.

## Security (money-critical)

- Anything that touches transaction bytes or signing must be tested against known-good vectors before it is trusted.
- Never commit secrets or private keys. `.gitignore` covers `*.key`, `.env`, `secrets.json`, `appsettings.*.local.json`.
- Never expose or export a raw private key to an RPC provider, hosted service, or third-party transaction builder. Keep signing behind `ISigner`; cryptographic backends are vetted implementation dependencies, not key-custody integrations. Simulate and assert instructions/amounts/destination before sending.

## Decisions

- `PublicKey` is a `readonly struct` backed by four `ulong` words (32 bytes inline, value equality, no per-key heap allocation). Base58 is cached only when the key is built from a string; from-bytes stays allocation-free. No zero-copy `AsSpan()` by design — use `CopyTo` / `ToBytes`.
- `Commitment` serializes via a custom `JsonConverter` applied as a `[JsonConverter]` attribute. The attribute makes it self-serializing under default options, not just `SolanaJsonSerializer.Options`, and preserves the existing wire contract independently of serializer enum policy.
- Wire enums/types follow that same pattern: self-serializing via attribute so they hold their wire form regardless of which `JsonSerializerOptions` are in play.
- **JSON is source-generated; reflection serialization is banned in src.** All RPC/WS paths go through the internal `RpcJson.Options` (resolver: `JsonTypeInfoResolver.Combine(SolanaJsonContext, CoreJsonContext)`; `SolanaJsonContext` in `Rpc/Protocol/` holds the closed root registrations); Core's public `SolanaJsonSerializer.Options` covers only the Core primitives via the public `CoreJsonContext`, with no reflection fallback. GOTCHA: a source-gen context can only materialize a converter-attributed type if it can construct the converter - an inaccessible converter makes the generator drop the type (SYSLIB1220 + SYSLIB1030; warnings locally, errors under CI's `-warnaserror`) and every use fails at runtime with `NotSupportedException`. That is why converter-attributed Core wire converters such as `CommitmentJsonConverter`, `PublicKeyJsonConverter`, and `HashJsonConverter` are **public**: keep these converters public, and keep `CoreJsonContext` in the chain. Consequences: request `params` entries are object-typed and dispatch by **exact runtime type**, so every boxed shape (configs in `Protocol/RpcParams.cs`, primitives, arrays — collections are pinned with `ToArray()`) must be registered in the context; anonymous types cannot be used in requests; a new `SendAsync<T>`/subscription/batch root type must be added to `SolanaJsonContext` (unregistered types throw `NotSupportedException`, which the offline client tests catch); types behind hand-written converters are invisible to the generator's graph walk, so what a converter reads via `options.GetTypeInfo<T>()` needs explicit registration. Every production project sets `IsAotCompatible` — the trim/AOT analyzers plus `-warnaserror` reject `RequiresUnreferencedCode`/`RequiresDynamicCode` APIs (e.g. `JsonSerializer.Serialize(..., options)` overloads, `ValidateDataAnnotations`). CI consumes the generated nupkg for its native smoke test, so the packaging layer is covered too.
- **Ed25519 lives in `Wallet` on `BouncyCastle.Cryptography`** — not the .NET BCL (.NET 10 ships no usable cross-platform `Ed25519`: Windows unsupported, Apple's is non-conformant) and not a hand-rolled curve. Pure-managed/portable was chosen over libsodium/NSec's native dependency, since signing throughput is not the bottleneck; `ISigner` keeps the backend swappable.
- **BLS12-381 lives in `Wallet` on `Nethermind.Crypto.Bls` 1.1.0 / Supranational `blst`** — the pinned Solana min-pk POP ciphersuite is implemented over a vetted native backend, never hand-rolled. Parse and verify paths require canonical subgroup points and reject infinity; secrets are canonical little-endian scalars and are zeroed. Supported packaged RIDs are Linux x64/arm64, macOS x64/arm64, and Windows x64; keep the facade dependency and `THIRD_PARTY_NOTICES.md` in sync.
- `Keypair` is one word to match the Solana ecosystem (`solana-keygen`, web3.js `Keypair`), not .NET's `KeyPair`. It stores only the 32-byte seed, derives the public key once, and zeroes the seed on `Dispose`.
- Solana version-0 signed off-chain messages live in `Wallet`: they use the pinned SDK's exact
  `0xffsolana offchain` domain, canonical bounded ASCII/UTF-8 formats, SHA-256 hashing, and Wallet's
  Bouncy Castle-based Ed25519 signer/verification path. The pathological-encoding verifier divergence is
  recorded in `docs/RUST_PARITY.md`. They are not transactions and examples must not imply on-chain authority.
- Transactions support **legacy**, **v0**, and feature-gated **SIMD-0385 V1** messages behind `ITransactionMessage`. Account ordering matches the pinned Solana SDK (fee payer first, then public-key byte order within writable-signer / readonly-signer / writable / readonly classes). v0 drains eligible accounts into lookup tables and prefixes `0x80`; V1 prefixes `0x81`, keeps all addresses inline, carries an inline execution config, and writes the message before its fixed number of signatures. V1's omitted compute/data limits mean zero and cluster activation is external, so examples must set deliberate limits and never imply universal availability. All formats use exact pinned Rust vectors.
- `PublicKey.IsOnCurve` is field arithmetic over BouncyCastle's `X25519Field`, **not** BouncyCastle's public-key validation: BC's validation rejects non-canonical encodings (y ≥ p) that Solana's `curve25519-dalek` accepts after reducing mod p, so the check is written directly as `SqrtRatioVar((y²−1)/(dy²+1))` with the top bit masked and no canonicality test. Solders-derived edge KATs and a deterministic corpus checked against an independent BigInteger/Legendre-symbol oracle pin the network semantics. Using `X25519Field` rather than `BigInteger.ModPow` is what makes it ~39× faster; keep the reduction semantics if that code is ever touched.
- **SPL Token account state uses the fixed-size `Pack` layout, not Borsh.** `Mint` (82 bytes) and `TokenAccount` (165 bytes) read a `COption` as a 4-byte little-endian tag followed by an *always-present* value (the slot is reserved even when `None`) — unlike Borsh's 1-byte tag with the value present only when `Some`. So `BorshReader` / `BorshWriter` are for Anchor/Borsh data; the SPL decoders are hand-written against the Pack layout and KAT'd against `solders.token.state`. (The Token *instruction* data is different again: a minimal `COption` of a 1-byte tag plus the value only when `Some`.)
- Money-critical encodings (message/transaction serialization, instruction data, PDA/ATA, on-curve) are checked byte-for-byte against `solana-sdk` (solders) and `solana-py`, not just round-trips.
- **Ships as one NuGet package.** The source stays four layered projects (so the compiler keeps Core crypto/IO-free, Wallet owns Ed25519, etc.), but only the `src/SolSharp` facade is packable: it references the four with `PrivateAssets="all"` and an MSBuild target (`BundleProjectReferences`) folds their DLLs + XML docs into a single `SolSharp` package, re-declaring the real third-party deps (kept in sync by hand). PDBs are embedded (`DebugType=embedded`) so symbols ride inside the bundled DLLs rather than a near-empty `.snupkg`. Default-`false` `IsPackable` (overridden only for `MSBuildProjectName == SolSharp`) keeps every other project from emitting its own package. Package validation compares the packed public API with the previous stable `PackageValidationBaselineVersion`; update that baseline deliberately when preparing each release.
- **NuGet graphs are locked per project.** Keep every committed `packages.lock.json` synchronized with its
  project and use `--locked-mode` in ordinary CI/release restores. The AOT smoke keeps its remote package
  graph and content hashes in `packages.packed.lock.template.json`; CI and release render a temporary copy by
  replacing only the job-built SolSharp package's version and SHA-512 content hash, then restore in locked
  mode. Keep the template synchronized with dependency, SDK, target-framework, and RID changes. The rendered
  lock belongs in runner temp storage, never in the committed project-reference lock. Native AOT publish must
  reuse that rendered lock and retain its implicit restore because the SDK resolves the Native AOT runtime pack
  during publish.
- **SDK 10 package archives are not byte-reproducible.** The release workflow therefore attests and stages the
  exact `.nupkg` in a durable draft GitHub Release before NuGet publishing. Retries recover those canonical
  bytes; never replace that ordering with a fresh post-publish `dotnet pack` result.
