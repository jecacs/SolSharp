# Changelog

All notable changes to SolSharp are documented here. The format is loosely based on
[Keep a Changelog](https://keepachangelog.com), and the project follows
[semantic versioning](https://semver.org) — from 1.0.0 breaking changes only come with a major
version (on the earlier 0.x releases, minor versions could carry them).

## [Unreleased]

## [3.2.0] - 2026-08-29

### Fixed

- WebSocket PubSub now fans an Agave-deduplicated server subscription out to every matching local
  subscriber and sends the server unsubscribe only after the final local subscriber ends. Duplicate
  acknowledgements no longer tear down the physical connection or drop sibling notifications.
- Transfer-hook validation-account decoding now validates the complete TLV buffer and rejects partial
  trailing POD entries before a transaction is built, matching the on-chain resolver's accept/reject rules.
- Alpenglow genesis certificates accept the full 515-byte Base2 signer-store envelope; malformed account
  responses and transaction-error numeric overflows now consistently surface as `JsonException`.
- Batched RPC responses now isolate a malformed, attributable entry to its own queued call while retaining
  batch-wide rejection for unknown, duplicate, or uncorrelatable response identifiers.
- Legacy message compilation snapshots each instruction's accounts and data before both compilation passes,
  and PDA bump search now matches the runtime's inclusive 255-through-1 range.

### Changed

- Upgradeable-loader state decoding now explicitly follows the raw runtime's fixed Buffer and ProgramData
  payload offsets (37/45), including immutable accounts; the compact bincode and `jsonParsed` client-decoder
  offsets (5/13) remain documented as a deliberate contract difference.
- The parity matrix and public verification documentation now record Wallet's pathological-point Ed25519
  divergence from pinned `verify_dalek`, plus the safe-direction strictness of transaction and off-chain
  message parsing. Contributor and release documentation now accurately describes the exact SDK and CI gates.

### Tests

- Added pinned known-answer and rejection vectors for upgradeable-loader states, Ed25519 curve boundaries,
  Unicode BIP-39 seeds, signer-derived BLS keys, Token-2022 confidential instructions, transfer-hook PDAs,
  malformed RPC models, batch isolation, and WebSocket subscription coalescing/lifecycle races.

## [3.1.0] - 2026-08-11

### Added

- `Mints.Token2022NativeMint`, the Token-2022 native mint (`9pan9bMn5HatX4EJdBwg9VgCa7Uz5HL8N1m5D3NdXejP`),
  a program address of the seeds `"native-mint"` and `255` under Token-2022. It is a different account from
  the classic `Mints.WrappedSol`.
- Length-bounded `Base58.Decode` and `Base58.TryDecode` overloads, plus `PublicKey.MaxBase58Length` (44),
  `Hash.MaxBase58Length` (44), and `Signature.MaxBase58Length` (88).
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, a pull-request template, and issue templates.
- `BannedSymbols.txt`, enforced as a build error by `Microsoft.CodeAnalysis.BannedApiAnalyzers` (RS0030).
  It currently carries the repository's permanent `ConfigureAwait` ban across every `Task`, `Task<T>`,
  `ValueTask`, `ValueTask<T>`, `IAsyncEnumerable<T>` and `IAsyncDisposable` overload, so the rule is
  enforced by the compiler rather than by review. The analyzer is development-only and is not a dependency
  of the published package.

### Performance

- `PublicKey.IsOnCurve` now uses the field arithmetic already present in the BouncyCastle dependency
  (`X25519Field.SqrtRatioVar`) instead of two 255-bit `BigInteger.ModPow` operations: **309 µs → 7.8 µs
  (39×)**. Program-derived and associated-token-account derivation inherit it — `GetAddress` goes from
  642 µs to 18 µs (**35×**), so 10,000 ATA derivations drop from 5.7 s to 0.18 s. Results are unchanged:
  verified identical on 200,000 random keys plus the non-canonical `y >= p` boundary, and all 300
  reference program addresses still match the pinned Rust `find_program_address` byte for byte.
- Base58 encoding of 32-byte inputs and `PublicKey`/`Hash` parsing use a fixed-width codec instead of the
  general byte-at-a-time bignum: **encode 3.0 µs → 0.53 µs**, **`PublicKey.TryParse` 1.18 µs → 0.25 µs**,
  and parsing no longer allocates an intermediate array. Output and accept/reject behaviour were verified
  identical to the general codec across 300,000 values, including leading-zero, all-zero and over-32-byte
  inputs. General `Base58.Decode`/`Base58.TryDecode` and encoding of other input sizes continue to use the
  general codec unchanged.

### Fixed

- WebSocket notification budgets charged the re-encoded decoded message rather than the bytes that arrived.
  UTF-8 decoding replaces every invalid byte with U+FFFD, which re-encodes to three bytes, so a peer could
  make the client charge up to three times the traffic it actually sent — against a budget whose limit is
  expressed in wire bytes. The transport now carries the exact received length alongside the decoded text
  and that length is what is charged.
- The AOT smoke restore passed `--force-evaluate` together with `--locked-mode`. `--force-evaluate` is the
  documented way to re-evaluate and rewrite a lock file, so it cancelled locked mode and the rendered
  package lock was regenerated instead of enforced.
- Locked restores broke whenever .NET shipped an SDK patch. `IsAotCompatible` and `PublishAot` inject
  `Microsoft.NET.ILLink.Tasks` and `Microsoft.DotNet.ILCompiler` at versions taken from the SDK's runtime,
  and both are recorded in `packages.lock.json`, so a floating SDK produced NU1004 with no source change.
  `global.json` now pins one exact SDK (10.0.303) with `rollForward: disable`; every workflow job that
  restores or builds the checkout installs it through `global-json-file` and asserts the resolved SDK
  equals that pin. The checkout-free publish job only verifies and pushes staged artifacts, so it pins its
  independently installed 10.0.x SDK in an isolated temporary `global.json` instead.
- `Token2022Program.CreateNativeMint` named the classic SPL Token wSOL mint as account 1 instead of the
  Token-2022 native mint, so the instruction was always rejected with `InvalidMint`. Verified against the
  address derived from the pinned interface's seeds.
- `SlotHashesSysvarState.Parse` and `StakeHistorySysvarState.Parse` rejected the canonical sysvar account
  whenever its 512-entry ring was not yet full. The runtime allocates these accounts at their canonical size
  and serializes into them, so a partly-filled ring is followed by zero padding; both decoders now accept
  that padding, still reject non-zero trailing bytes, and bound the input to the canonical account length.
  This affected fresh local validators and any cluster younger than 512 epochs, not mature clusters.
- Fixed-width base58 parsing now bounds public keys, hashes, signatures, keypairs, and legacy/v0 recent
  blockhashes before decoding. Base58 decoding is quadratic, so a hostile 200,000-character string previously
  took roughly 24 seconds to reject; it is now rejected immediately. Public-key and hash JSON converters reject
  oversized raw tokens before materializing them, commitment JSON is compared without creating a string, and
  validation exceptions no longer echo untrusted input.
- Secret-key hex import now decodes through a fixed zeroed buffer instead of creating an immutable string copy
  and an input-sized byte array. Ed25519 and BLS JSON key imports reject representations above 4 KiB before
  materializing their integer arrays, bounding allocations from malformed or hostile key files.
- BLS signer-based derivation reports a null signature from a custom signer as malformed instead of leaking a
  `NullReferenceException` through the public API.
- Transfer-hook extra-account resolution rejects more than 256 metadata entries before allocating or fetching,
  and fetches account data only when a later seed actually reads it. A hostile validation account could
  previously amplify one operation into tens of thousands of sequential resolver/RPC calls.
- WebSocket subscription queues now enforce wire-byte budgets as well as item counts: 64 MiB per
  subscription and 256 MiB per client by default, both configurable, and the per-subscription limit may not
  exceed the total. Reservations survive completion and reconnect while unread values remain buffered.
  They are released as values are read, and deterministically on teardown for the `IAsyncEnumerable`
  subscriptions. The subscriptions that hand back a `ChannelReader` cannot observe an abandoned reader, so
  a caller that stops reading without draining releases its reservation only when the reader is finalized.
  Note that the total budget is a whole-client circuit breaker: once it is exhausted, the next subscription
  to receive a notification is the one that faults, which is not necessarily the one holding the bytes.
- Legacy and v0 message compilation validates null instruction state and compact-u16 collection limits before
  copying, and serialized-length calculations now fail deterministically on integer overflow.
- `TransactionFailedException` now owns a clone of its structured JSON error, so the public `Error` property
  remains usable after the source `JsonDocument` is disposed.
- `AssociatedTokenAccount.DecodeInstructionData` returned `null` for empty instruction data. The program
  decodes an empty input as `Create`, which is the original ATA encoding still present in historical
  transactions.
- The `AddSolanaRpc` resilience defaults allowed each attempt only 10 seconds, which aborted heavy but valid
  reads (large `getProgramAccounts`, full `getBlock`) that the reference client completes within its 30-second
  per-request budget. The per-attempt budget now matches the reference and the total request budget was
  widened so retries still fit; an explicit `configureResilience` callback continues to override both.

## [3.0.0] - 2026-08-10

### Changed

- Retargeted the SolSharp NuGet package and every library, test, benchmark, and Native AOT sample project
  from `net8.0` to `net10.0`. Source builds now select the stable .NET 10 SDK line and C# 14 with preview
  SDKs disabled.
- Modernized implementation code for C# 14, including compiler-backed property storage and
  `System.Threading.Lock`, while preserving the existing protocol-focused scope.
- Updated dependency locks and the CI, security, CodeQL, release, package-validation, and packed-package
  Native AOT paths to use and verify .NET 10 consistently.
- Enforced deterministic member ordering for fields, constructors, properties, methods, and nested types,
  including public-to-private accessibility and constant/static/readonly precedence.
- Aligned Rider/ReSharper with Roslyn's target-typed object-creation style so repository rules override
  conflicting developer-level IDE preferences.

### Fixed

- Preserved BLS secret-key derivation under C# 14 by explicitly selecting the backend's mutable-span
  constructor; the new overload resolution otherwise treated the zeroed output buffer as encoded key material.

### Breaking changes

- SolSharp 3.0.0 no longer contains a `net8.0` asset. Consumers must target `net10.0` (or a later compatible
  .NET target) and recompile before upgrading.

## [2.0.0] - 2026-08-09

### Added

- Published `docs/RUST_PARITY.md` with immutable Anza Solana SDK, Agave, System, ALT, SPL Token,
  Token-2022, and ATA source pins, explicit client/runtime boundaries, and release verification gates.
  `THIRD_PARTY_NOTICES.md` records attribution and is included in the NuGet package.
- Added distinct `Hash` and `Signature` value types with exact byte/base58 semantics, typed blockhash and
  signing overloads, strict signature verification, verified external `Presigner` support, and `NullSigner`
  placeholders for multi-stage/offline signing. Transactions now expose ordered typed signature slots and
  required keys, explicit `PartialSign` / `SignAll`, verified `AddSignature`, completeness and per-slot
  verification, exact signable bytes, and verify-and-hash. `TransactionMessageHash` computes the Rust SDK's
  domain-separated BLAKE3 message hash and is checked against its upstream known-answer vector.
- Added the pinned Solana SDK version-0 `OffchainMessage` contract: canonical ASCII/UTF-8 formats,
  domain-separated serialization and SHA-256 hashing, strict bounded parsing, typed signing, verification,
  and exact upstream vectors. `Keypair` can now deliberately export the Rust/wallet 64-byte form, its
  32-byte seed, base58, or `solana-keygen id.json`, with defensive byte copies and secret-cleanup guidance.
- Added the pinned minimal-public-key-size BLS12-381 proof-of-possession scheme used by Vote v2/v4:
  validated compressed public keys/signatures/proofs, deterministic and signer-derived keys, vote-account
  proof binding with pre-serialization validation, exact Rust-compatible 128-byte/zeroable UTF-8 JSON key
  files, strict allocation-bounded base64, PoP-provenance-gated same-message key/signature aggregation,
  upstream vectors, native-AOT coverage,
  and typed Vote builder overloads. The packaged native backend supports Linux x64/arm64, macOS x64/arm64,
  and Windows x64; subgroup, infinity, canonical-secret, and secret-cleanup checks are enforced locally.
  Matching the Rust SDK's rogue-key boundary, raw public keys validate proofs but signature verification is
  exposed only through derived keypairs or proof-of-possession-verified wrappers.
- Added the feature-gated SIMD-0385 V1 message and transaction format: inline execution configuration,
  `0x81` version routing, message-first/fixed-signature framing, compile/sanitize/serialize/deserialize/decompile,
  version-aware builders, and exact pinned Rust wire vectors. The client documents cluster activation and
  zero-valued omitted compute/data limits rather than implying universal runtime availability.
- Added System `create_with_seed` address derivation, `SystemProgram.CreateAccountAllowPrefund`, and
  `AssociatedTokenAccount.RecoverNested`, including upstream vectors and exact signer/writable layouts.
  `SystemProgram.TransferMany` and `CreateNonceAccountWithSeed` mirror the remaining stable client helpers.
- Completed the classic SPL Token instruction family with the current mint/account/multisig initializers,
  account-size and UI-amount queries, immutable-owner and sync-native variants, excess-lamport withdrawal,
  unwrap, and batch instructions.
- Added typed Token-2022 construction for base extension allocation, transfer fees, default account state,
  required transfer memos, CPI guard, interest-bearing mints, transfer/metadata/group pointers, scaled UI
  amounts, pausing, checked and confidential permissioned burns, and the Token Metadata interface.
- Added pinned native-program clients for Stake and Vote (including compact/tower and V2/BLS layouts),
  legacy/upgradeable/V4 loaders, Ed25519/Secp256k1/Secp256r1 verification precompiles, Address Lookup
  Table state (including SlotHashes-aware activation, active-prefix, and lookup semantics), raw memo bytes,
  and a bounded Instructions sysvar constructor/decoder for off-chain instruction introspection. Strict
  account decoders cover stake, loader, ALT, and feature state.
- Added all current sysvar IDs and bounded decoders for Clock, Rent, EpochSchedule, EpochRewards,
  LastRestartSlot, SlotHashes, SlotHistory, and StakeHistory, plus strict version-preserving Vote
  V1.14.11/V3/V4 account-state decoders and Feature Gate activation/revocation clients.
- Added SPL token-group and member builders/state, transfer-hook validation PDA and extra-account-meta
  codecs/resolution, confidential-transfer/fee/mint-burn instruction families, native proof-program POD
  instructions, ElGamal registry state, and typed classic/Token-2022 instruction, base-account, metadata,
  group, hook, and extension decoders. Cryptographic proof/ciphertext generation remains explicitly
  caller-supplied rather than being replaced by an unverifiable local implementation.
- Added a forward-compatible local `global.json`; after `setup-dotnet` installs 8.x, every CI/release job
  that invokes .NET asserts the SDK resolver's actual selected version, so preinstalled newer SDKs cannot
  invalidate the minimum-SDK gate. Release publishing now fails unless the pushed tag exactly matches the
  package version, requires private live-cluster endpoints, and admits no skipped or inconclusive unit,
  read, or streaming paths. Live HTTP reads and devnet writes use test-only two-request-per-second limiters,
  while WebSocket probes run serially with paced starts so low-tier provider quotas do not turn a sequential
  suite into a burst. Faucet-dependent devnet writes remain a separate probe: deterministic failures still
  block publication, while classified faucet/rate-limit failures are reported as inconclusive instead of
  making release availability depend on the shared faucet. The probe verifies the canonical devnet genesis
  hash before any write, and Native-AOT publishes and runs the exact packed artifact from an isolated package
  cache before pushing it. A duplicate immutable NuGet version succeeds only when the repository-signed NuGet
  copy proves it contains the exact pre-staged canonical package; mismatched or unverifiable duplicates fail
  visibly.
- Added centrally configured StyleCop analysis to every project. Rider, Roslyn, and StyleCop now share an
  explicit modifier order, while repository-conflicting documentation and legacy-layout rules are suppressed
  in `.editorconfig` instead of producing misleading IDE warnings. CI and release now require a clean
  solution-wide `dotnet format --severity info` analyzer pass in addition to the warning-free build.
- Added merged unit-test coverage reporting: 93.7% line coverage across the four
  hand-written production assemblies, with generated sources excluded, full branch details published, and
  a 90% line gate. Scheduled direct/transitive NuGet auditing, pull-request dependency review, CodeQL
  `security-extended`, Dependabot updates, OpenSSF Scorecard reporting, Node 24 action pins, and release-package
  provenance attestations harden the GitHub supply chain without representing automated results as an
  independent security audit.
- Added committed per-project NuGet lock files and locked CI/release restores. The dynamic packed-package
  AOT consumer keeps its job-specific lock under `obj` and separately proves that it restored the exact
  package produced by the job.
- Added five deterministic FsCheck properties covering 5,000 bounded arbitrary, truncated, and
  single-byte-overwrite legacy/v0/V1 transaction cases per run. Accepted bytes must round-trip exactly, and
  rejected bytes must fail through the documented format boundary.
- Split release validation from narrowly privileged attestation, draft-staging, NuGet-publishing, and
  finalization jobs. The contents-only staging job durably records the exact attested package before the
  OIDC publisher sends those same bytes to NuGet; a separate contents-only finalizer publishes the verified
  draft with the `.nupkg`, SHA-256 digest, and Sigstore/SLSA provenance bundle.
- SPL Token and Token-2022 authority-bearing instruction builders now have additive multisig overloads:
  the multisig authority remains a non-signer account and the supplied member accounts are appended as
  readonly signers in caller order.
- `RpcException.ErrorData` preserves the optional JSON-RPC `error.data` payload (including preflight logs
  and units consumed). `SolanaWsClientOptions.SubscriptionAckTimeout` bounds initial and replayed
  subscription acknowledgements, while `MaxPendingSubscriptionRequests` caps live ACK waits plus compact
  late-ACK cleanup records.
- Added the current Agave `getAgGenesisCert` read as `GetAgGenesisCertificateAsync`, including typed
  Alpenglow block-certificate and aggregate-signature models.
- Added source- and binary-compatible explicit maximum-version reads and block subscriptions. Raw and parsed
  HTTP/WebSocket paths can opt into V1, parsed messages preserve Agave's inline `transactionConfig`, and the
  existing method names remain pinned to legacy/v0 for behavior compatibility.
- Completed the effective pinned HTTP/PubSub configuration surface through source-safe, explicitly named
  options/filter methods: minimum-context-slot reads; the full legacy/base58/base64/jsonParsed/base64+zstd
  account-data union; HTTP account/program data slices and context-wrapped scans; mint-or-program token filters;
  the full program-account filter union (base58/base64/raw memcmp, unsigned 64-bit data size, and
  `tokenAccountState`) with pinned validation limits;
  sortable supply/leader/vote options; raw block/transaction encoding-detail-reward choices; exact logs/block
  filter unions; parsed program subscriptions; and the optional early `receivedSignature` notification before
  final processing. `SubscribeAccountWithOptionsAsync` and `SubscribeProgramWithOptionsAsync` expose only the
  encoding/commitment/filter fields that pinned Agave actually applies and preserve the exact account-data
  response union, including unknown-program `jsonParsed` fallback and `base64+zstd`.
- `SimulateTransactionOptions` can now request post-simulation account snapshots in the full effective
  base64, base64+zstd, or jsonParsed account-data union and parsed inner
  instructions. Simulation and transaction models preserve current node fields including transaction
  version/index, cost and loaded-data units, fees, balances, return data, rewards, loaded addresses,
  parsed v0 lookup references, RPC API version, validator endpoints/client id, and basis-point commissions.
- Added `SystemProgram.UpgradeNonceAccount`, matching the generated System Program client's discriminator
  and account layout for migrating legacy nonce state.

### Changed

- Blockhash and durable-nonce APIs now accept the typed `Hash` value alongside their existing string forms.
  Existing calls that pass an untyped `null` or `default` literal must cast it to `string` (or use a typed
  `Hash`) to disambiguate overload resolution; ordinary string and `Hash` calls are unchanged.
- RPC models now use the pinned wire widths and closed unions instead of permissive signed/JSON containers:
  `DataSlice` offsets and lengths are `ulong`; transaction versions use `RpcTransactionVersion`; vote epoch
  credits and block-production counts are exact typed tuples; transaction indexes use `byte`/`uint`; and
  simulation account snapshots use the lossless `RpcAccountInfo` union. This is a deliberate 2.0 source and
  binary migration for callers that stored these fields in the former broad types; assemblies built against
  1.x must be recompiled for 2.0.

### Fixed

- Serialized secret-dependent Ed25519/BLS keypair operations against disposal, so concurrent disposal can
  no longer zero a key while it is being signed or exported. Typed Vote BLS credentials now return defensive
  copies and serialize through private validated bytes, preventing public memory views from mutating a
  proof-of-possession-checked instruction after validation. Secret JSON-export temporaries are cleared on
  allocation and serialization failures as well as successful completion.
- Bounded Token-2022 metadata vector counts before allocation, preventing a malformed four-byte Borsh
  length from requesting a multi-gigabyte `List` capacity.
- Matched precompile runtime count semantics: Ed25519 and Secp256r1 offset tables use the first header
  byte, Secp256r1 accepts only 1-8 signatures, and zero-count trailing-data cases are rejected instead of
  producing or decoding instructions that Agave refuses.
- Kept the durable nonce account in v0 static keys even when it also appears in an Address Lookup Table,
  matching the Solana SDK and preserving runtime durable-nonce recognition, including valid advance-nonce
  instructions with trailing data. Durable-nonce-only legacy and v0 builders are now accepted.
- Matched current canonical program builders: Address Lookup Table creation no longer requires the future
  authority to sign, Associated Token Account creation emits its explicit `[0]` discriminator, memo text
  rejects invalid Unicode instead of encoding replacement characters, and undefined Token/Token-2022
  authority discriminators are rejected before serialization.
- Hardened Ed25519 verification against small-order public-key/signature points accepted by the underlying
  crypto backend, closing a signature-malleability edge while retaining Solana-compatible mixed-torsion
  behavior. Secret-key public-half validation is constant-time, and SLIP-0010 paths now reject signed or
  whitespace-padded numeric segments.
- Preserved parameter payloads for all current transaction-error variants instead of dropping their
  instruction/account indexes, and retained the current optional fields returned by account, transaction,
  simulation, cluster-node, inflation, token, and parsed-message RPC responses.
- Validated account ownership, executable state, canonical fixed/TLV layouts, nonce versions, Address
  Lookup Table option tags/padding/address alignment, and classic Token account sizes before typed decode.
  Confirmation polling now falls back to the upstream `confirmations` semantics when an older node omits
  `confirmationStatus`.
- Address Lookup Table reads now retain the RPC context slot, the full stored address list, and the
  `last_extended_slot_start_index`; transaction-facing addresses exclude same-slot additions, while lifecycle
  and nullable usability distinguish active, cooling-down, and status-unknown deactivation states without
  incorrectly treating every requested deactivation as unusable.
- Added a configurable 128 MiB default limit for single and batch HTTP response bodies, enforced while
  streaming even when `Content-Length` is absent, so a provider cannot cause an unbounded response buffer.
- Hardened batch JSON-RPC handling: every entry must be a valid 2.0 envelope with one known, unique id and
  exactly one result or error member (including rejecting `result` together with `error: null`); malformed
  replies now terminate every queued task instead of leaving calls pending indefinitely.
- Serialized concurrent WebSocket connects, disposed sockets from failed initial/reconnect attempts,
  completed one-shot signature subscriptions after their notification, bounded subscribe acknowledgement
  waits, isolated cancellation and acknowledgement failures during reconnect replay, and bounded abandoned-ACK
  state so one failed subscription cannot stall the replay queue or leak retained subscription state.
- WebSocket routing now accepts the protocol's full unsigned 64-bit subscription-id range, returned channel
  subscriptions support concurrent consumers, and signature confirmation accepts arbitrarily long or
  infinite timeouts without overflowing the platform timer. Shared-socket sends isolate one subscriber's
  cancellation, duplicate server IDs fail the corrupted generation, and disposal completes the close handshake.
- WebSocket notifications now validate JSON-RPC versions and the subscription family's exact method before
  routing, reject null or incomplete context payloads without leaving readers pending, and isolate malformed
  scalar payloads to their own subscription. An unsolicited `receivedSignature` event can no longer satisfy a
  final-only confirmation, and a transport-originated cancellation no longer suppresses later reconnect attempts.
- Exact account responses now require every mandatory upstream field, context wrappers require context/value/slot
  presence, and malformed single-call JSON-RPC error objects are rejected instead of fabricating code `0` and an
  empty message. The final guaranteed ALT cooldown slot (`deactivation_slot + 512`) remains classified as usable.
- `ConfirmTransactionAsync` now applies its timeout to an in-flight HTTP status request as well as the delay
  between polls. The DI resilience policy no longer retries non-idempotent `requestAirdrop` calls.
- Compiled messages defensively copy instruction data; transactions snapshot the bytes used for their first
  signature, validate custom signer output is exactly 64 bytes, and serialize those same signed bytes even if
  caller-owned message arrays are later mutated.
- Rejected unrepresentable signer counts before message header conversion, legacy messages whose signer-count
  high bit collides with the version prefix, trailing bytes in full message and transaction parsers, and
  System Program seeds longer than 32 UTF-8 bytes. Transaction builders now report null signer elements as
  argument errors instead of failing indirectly while selecting the fee payer.
- Made Borsh decoding canonical (`bool`/`Option` accept only `0` or `1`) and UTF-8 strict in both directions;
  malformed text is rejected instead of silently replaced. Core JSON converters now consistently throw
  `JsonException` for wrong token kinds and the source-generated Core context supports nullable public keys.
- Tightened secret-buffer cleanup in BIP-39 and key parsing exception paths.
- CI now validates the packed public API against 1.3.0 and runs the Native AOT smoke test against the actual
  generated NuGet package rather than direct project references.

## [1.3.0]

### Added

- `SolanaWsClientOptions.MaxMessageSizeBytes` (default 64 MiB), `SubscriptionBufferCapacity`
  (default 1,024), and `ReceiveTimeout` (off by default): bounds on incoming message size and
  per-subscription buffering, plus an opt-in idle timeout that lets auto-reconnect replace a silently
  half-open connection when the subscribed traffic is known to be frequent.
- `docs/USAGE.md`: examples for the previously unillustrated API — `AddSolanaWs` container registration,
  allocation-free serialization (`GetSerializedLength` / `TrySerialize` and the span `Serialize`
  overloads), pricing an unsigned message via `BuildMessage` / `BuildMessageV0`, the wider
  `SystemProgram` op set, the remaining nonce instructions (withdraw, authorize),
  `TryCreateProgramAddress`, and the explicit `Keypair` factory methods. README's `TransactionBuilder`
  bullet now names `BuildMessage` / `BuildMessageV0`.

### Changed

- Updated `Microsoft.Extensions.Http.Resilience` from 8.0.0 to 8.10.0, removing the
  transitive vulnerable `System.Text.Json 8.0.0` dependency.

### Fixed

- `SendTransactionAsync` preflight and `SimulateTransactionAsync` now run at `confirmed` commitment by
  default, matching `GetLatestBlockhashAsync` — at the node's own `finalized` default a just-fetched
  blockhash may not exist yet, failing preflight or simulation with `BlockhashNotFound` for a perfectly
  valid transaction. Set the option to `null` to fall back to the node default.
- Hardened the WebSocket transport with bounded message sizes, text-frame validation, a complete
  close handshake, and bounded disposal.
- Bounded each subscription notification buffer; a consumer that falls behind is now faulted and
  unsubscribed instead of allowing unbounded memory growth.
- A subscription cancelled while the connection was down — or while its reconnect replay was awaiting
  the node's acknowledgement — is no longer resurrected server-side: the late acknowledgement releases
  it. A notification racing a cancellation no longer faults the subscription with a spurious
  buffer-overflow error.
- Validated the JSON-RPC response envelope (protocol version, request-id echo, result/error presence)
  in a single parsing pass with no intermediate DOM, preventing malformed value-type responses from
  silently becoming `0`, `false`, or another default. A node-supplied error always surfaces with its own
  code and message — including spec-mandated `"id": null` error responses — instead of a generic
  envelope error.
- `Message.Deserialize` and `MessageV0.Deserialize` (and therefore `Transaction.Deserialize`) enforce
  Solana's `sanitize()` rules — header counts that overlap the account list or leave no writable
  fee-payer signer, out-of-range program-id and account indexes, an instruction whose program id is the
  fee payer, and for v0 additionally: program ids restricted to static keys, address-table lookups that
  load no accounts, and the 256-account ceiling — with the rule set verified against solders.
- Rejected malformed transaction data tuples and unexpected encodings instead of treating them as
  nullable or decoding non-base64 payloads.
- Included the Core, Programs, RPC, and Wallet XML documentation files in the bundled NuGet package.

## [1.2.0]

### Added

- `AddressLookupTableProgram.FreezeLookupTable` — the one Address Lookup Table instruction that was
  missing (discriminant 1): permanently locks a table immutable.
- `AuthorityType` now carries the full Token-2022 authority set (`TransferFeeConfig`,
  `WithheldWithdraw`, `CloseMint`, `InterestRate`, `PermanentDelegate`, `ConfidentialTransferMint`,
  `TransferHookProgramId`, `ConfidentialTransferFeeConfig`, `MetadataPointer`, `GroupPointer`,
  `GroupMemberPointer`, `ScaledUiAmount`, `Pause`, `PermissionedBurn`), numbered per
  `spl-token-2022`, so `TokenProgram.SetAuthority` can change extension authorities on Token-2022
  mints and accounts. The classic four variants are unchanged.

### Changed

- Signing and verification now use BouncyCastle's span-based Ed25519 APIs: `Keypair.Sign` no longer
  copies the message (the signature is the only allocation), `PublicKey.Verify` is allocation-free,
  and keypair construction derives the public key without an intermediate array. Behavior is
  unchanged; this trims per-transaction allocations on the hot signing path.

### Fixed

- `TransactionBuilder.SetRecentBlockhash` now clears a previously set durable nonce, dropping its
  prepended `AdvanceNonceAccount` instruction — mirroring how `SetDurableNonce` replaces a previously
  set blockhash. Before, switching back to blockhash anchoring silently left the advance-nonce
  instruction in the transaction, which would consume the nonce and demand the nonce authority's
  signature.
- `Transaction.Deserialize` now rejects (with `FormatException`) wire bytes whose signature count does
  not match the message's required signatures — the same rule Solana's sanitize step enforces. Before,
  such input parsed silently and a later `Sign` could fail with an unrelated
  `IndexOutOfRangeException`.
- `SolanaUnits.SolToLamports` now throws the documented `ArgumentOutOfRangeException` for amounts too
  large to express in lamports; very large inputs previously surfaced as decimal's
  `OverflowException`.

## [1.1.0]

### Added

- Two new WebSocket streams completing the subscription surface: `SubscribeSlotsUpdatesAsync`
  (`slotsUpdatesSubscribe` — every stage of the slot lifecycle: shreds received, bank created, frozen
  with per-slot transaction stats, optimistic confirmation, root, dead) and `SubscribeVotesAsync`
  (`voteSubscribe` — gossip votes before they land in a block; requires a node started with
  `--rpc-pubsub-enable-vote-subscription`). Both are parameterless `IAsyncEnumerable` streams like
  `SubscribeSlotsAsync`, and both are marked unstable by Solana — the wire shape can change between
  node versions.
- New notification models: `SlotsUpdate` (+ `SlotsUpdateStats`) and `VoteNotification`.

## [1.0.1]

Documentation-only release; no code changes.

### Fixed

- The NuGet package now carries a dedicated `README.nuget.md`: nuget.org renders a restricted
  markdown where raw HTML is shown as text, so the GitHub README's HTML-centered logo appeared as
  literal markup on the package page.

### Added

- `docs/USAGE.md` gains a **Publishing with Native AOT** section (enabling `PublishAot`, bringing
  your own `JsonSerializerContext` for your own models, chaining `CoreJsonContext`) and examples for
  the previously unillustrated reads: paging an address's signature history and walking a block,
  data-driven priority fees (`getRecentPrioritizationFees` + `getFeeForMessage`), token account
  balance and largest holders, and the node/cluster basics (health, version, block height,
  transaction count, supply, slot leaders, blockhash validity).
- README refreshed for the stable line: roadmap and per-assembly status column removed, Native AOT
  moved to the top of the pitch, `samples/` added to the layout.

## [1.0.0]

First stable release. The public API is now covered by the semver compatibility promise.

### Added

- The remaining Solana JSON-RPC HTTP read methods — `SolanaRpcClient` now covers the full current API
  surface (deprecated `getStakeActivation` deliberately excluded): `GetBlockCommitmentAsync`,
  `GetBlockProductionAsync` (identity/slot-range narrowing), `GetBlockTimeAsync`,
  `GetBlocksWithLimitAsync`, `GetEpochScheduleAsync`, `GetFirstAvailableBlockAsync`,
  `GetGenesisHashAsync`, `GetHighestSnapshotSlotAsync`, `GetIdentityAsync`,
  `GetInflationGovernorAsync`, `GetInflationRateAsync`, `GetLargestAccountsAsync` (with
  `LargestAccountsFilter`), `GetMaxRetransmitSlotAsync`, `GetMaxShredInsertSlotAsync`,
  `GetRecentPerformanceSamplesAsync`, `GetSlotLeaderAsync`, `GetStakeMinimumDelegationAsync`,
  `GetTokenAccountsByDelegateAsync`, and `GetMinimumLedgerSlotAsync` (wire method
  `minimumLedgerSlot`).
- New response models: `BlockCommitment`, `BlockProduction` (+ `BlockProductionRange`),
  `EpochSchedule`, `HighestSnapshotSlot`, `InflationGovernor`, `InflationRate`, `LargestAccount`,
  and `PerformanceSample`. `getIdentity` and `getSlotLeader` unwrap to `PublicKey` directly.

## [0.7.0]

*0.7.0 was folded into the v1.0.0 release — it has no separate tag or NuGet package.*

### Added

- Source-generated JSON serialization end to end: every RPC request, response envelope, and WebSocket
  notification now serializes through source-generated `JsonSerializerContext`s (all ~60 closed root
  shapes registered) instead of reflection — faster startup, no runtime metadata generation, and the
  whole request/response surface verifiable at compile time. The public `CoreJsonContext` exposes the
  metadata for the Core wire primitives (`Commitment`, `PublicKey`) for chaining into your own
  source-generated resolvers, and `CommitmentJsonConverter` / `PublicKeyJsonConverter` are now public —
  a source-generated context can only register a converter-attributed type when it can construct the
  converter, so consumer contexts registering models with these primitives need them accessible.
- Native AOT support: all four assemblies build clean under the trim/AOT analyzers and are marked
  `IsAotCompatible` (trimmable), so `PublishAot` applications can take SolSharp without warnings. CI
  gains an `aot-smoke` job that publishes `samples/SolSharp.AotSmoke` as a native binary and runs its
  offline signing, transaction-serialization, and JSON-RPC-pipeline checks.
- `DataSlice` is now self-serializing to its `{ offset, length }` wire shape via `JsonPropertyName`
  attributes, like the other wire types.

### Changed

- **Breaking:** `SolanaJsonSerializer.Options` no longer falls back to reflection. It is frozen over a
  source-generated resolver covering the Core wire primitives (`Commitment`, `PublicKey`); serializing
  any other type with it now throws `NotSupportedException`. For custom models, create your own
  `JsonSerializerOptions` — the SolSharp wire types keep their format under any options because their
  mappings live in `[JsonConverter]` attributes.
- **Breaking (DI edge case):** `AddSolanaRpc` validates `SolanaRpcOptions.Endpoint` with an explicit
  predicate instead of `ValidateDataAnnotations()` (which is not trim-safe); the same absolute-http(s)
  rule is enforced, and the `Microsoft.Extensions.Options.DataAnnotations` package dependency is gone.
- RPC request parameter objects are typed internal records instead of anonymous types (a source-generation
  requirement); the emitted wire bytes are unchanged and remain pinned by the request-body test asserts.

## [0.6.0]

### Added

- Token-2022 extension decoding: `TokenExtensionSet.DecodeMint` / `DecodeAccount` walk the TLV section of
  an extended mint or token account (layout and `ExtensionType` values mirrored from
  `spl_token_2022_interface`), with typed views for the transfer-fee config and withheld amounts, the
  metadata pointer and in-mint `TokenMetadata`, the permanent delegate, mint close authority, default
  account state, and the memo-transfer requirement — and raw TLV access for every other extension.
- A devnet write gate in the release workflow: a live airdrop → transfer → confirm round-trip and the full
  durable-nonce lifecycle (create, fetch, nonce-anchored spend, advance) now run against devnet before
  anything is published. The write suite never targets mainnet; `SOLSHARP_DEVNET_RPC_URL` overrides the
  endpoint.
- JSON-RPC batching: `SolanaRpcClient.CreateBatch()` queues calls (`GetBalanceAsync`, `GetAccountInfoAsync`,
  `GetLatestBlockhashAsync`, `GetSlotAsync`, `GetTokenAccountBalanceAsync`, `SendTransactionAsync`) and
  submits them in one HTTP round-trip with `ExecuteAsync`. Responses are matched by id in any order; a
  per-call node error faults only that call's task.
- Allocation-free serialization: `GetSerializedLength()` and a span-writing `Serialize(Span<byte>)` on
  `Message` / `MessageV0` (and `ITransactionMessage`), plus `Transaction.TrySerialize(Span<byte>, out int)`
  for latency-sensitive senders. The allocating `Serialize()` overloads now produce exactly one
  right-sized array (byte output unchanged, still KAT-verified).
- `AddSolanaWs(...)` registers `SolanaWsClient` as a container-managed singleton wired to the registered
  `ILoggerFactory`.
- A BenchmarkDotNet harness under `benchmarks/` (signing, transaction compile + serialize, base58,
  `jsonParsed` decoding); run it with `dotnet run -c Release --project benchmarks/SolSharp.Benchmarks`.

## [0.5.0]

### Added

- Durable-nonce support end to end: a `NonceAccount` decoder and `SolanaRpcClient.GetNonceAccountAsync`,
  the `SystemProgram.CreateNonceAccount` create-and-initialize pair (plus `NonceAccountLength`), and
  `TransactionBuilder.SetDurableNonce`, which anchors the transaction to the nonce value and prepends the
  required `AdvanceNonceAccount` instruction.
- Mnemonic wallet import: `Keypair.FromMnemonic` (the `solana-keygen` scheme) and
  `Keypair.FromMnemonicAtPath` (the Phantom / Solflare SLIP-0010 scheme, `m/44'/501'/account'/0'`), built on
  the new public `Bip39.ToSeed` and `Slip10.DeriveEd25519` - validated against the official SLIP-0010 and
  Trezor BIP-39 test vectors.
- System program: `AllocateWithSeed`, `AssignWithSeed`, and `TransferWithSeed`.
- SPL Token: `SetAuthority` (with the `AuthorityType` enum) and the checked variants `ApproveChecked`,
  `MintToChecked`, and `BurnChecked`.
- Compute Budget: `RequestHeapFrame` and `SetLoadedAccountsDataSizeLimit`. Associated Token Account:
  `CreateIdempotent`.
- XML docs for the previously undocumented public constants (`SolanaProgramIds`, `Mints`, `Sysvars`, the
  `TokenProgram` instruction discriminators) and `SolanaJsonSerializer.Options`. Missing public XML docs now
  fail the CI build for library code: `CS1591` is suppressed only in test projects.

### Fixed

- `SolanaWsClient` now surfaces JSON-RPC **error responses** to subscribe calls. Previously an error frame
  (`{"id":N,"error":{...}}`) matched no routing branch and was dropped, so a rejected subscription left the
  `Subscribe*Async` call awaiting its acknowledgement forever — a silent hang instead of an exception. The
  rejection now faults the call with an `InvalidOperationException` carrying the node's error code and
  message, and is logged at Warning level.
- A notification that fails to decode now faults only its own subscription: its channel or stream completes
  with the decode error and the subscription is unsubscribed, while the connection and every other
  subscription keep going. Previously the exception escaped the receive loop and read as a dropped
  connection, tearing down — or, with auto-reconnect and a systematically undecodable payload, endlessly
  re-establishing — every subscription on the client.
- `SolanaWsClient.DisposeAsync` now completes every active subscription's channel and stream, so a consumer
  blocked on a read observes an orderly end of stream instead of hanging forever; a subscribe still awaiting
  its acknowledgement faults with `ObjectDisposedException`. Dispose is also safe to call more than once.
- `SolanaWsClient.ConnectAsync` now throws `InvalidOperationException` when the client is already connected
  (previously a second call silently started a competing receive loop) and `ObjectDisposedException` after
  disposal.
- `MessageV0.Deserialize` now rejects versioned messages whose version is not 0 instead of silently
  misparsing a future format as v0, and `Message`/`MessageV0`/`Transaction.Deserialize` throw the documented
  `FormatException` on truncated input instead of leaking index exceptions.
- PDA derivation now enforces solana-sdk's 16-seed limit (the new `ProgramDerivedAddress.MaxSeeds`):
  oversupplying seeds throws `ArgumentException` instead of deriving an address the runtime would reject.
- `MessageV0.Compile` now rejects an address lookup table holding more than 256 addresses — which the
  single-byte wire indexes cannot address — instead of silently truncating them.

### Changed

- `MemoProgram.Memo` now references memo signers as **read-only** signers, matching the canonical Rust
  `spl-memo` builder (`AccountMeta::new_readonly(pubkey, true)`); they were previously writable, which
  needlessly write-locked the signer accounts. The compiled bytes of a transaction change only when a memo
  signer is not already writable elsewhere in it.

## [0.4.1]

### Fixed

- `jsonParsed` transactions no longer fail to decode when an instruction's `parsed` field is a bare value
  instead of a `{ type, info }` object (spl-memo returns the memo string); the value is preserved on
  `ParsedInstructionInfo.Info` with an empty `Type`.

## [0.4.0]

### Added

- Optional `ILogger` diagnostics for `SolanaWsClient`: pass an `ILoggerFactory` to the constructor to surface
  connection drops, reconnects, subscription replays, and unsubscribe/dispose failures that were previously
  swallowed. Defaults to a no-op `NullLogger`, so behaviour is unchanged when no factory is supplied.

### Changed

- The JSON-RPC envelope plumbing — `RpcRequests`, `RpcRequest`, `RpcResponse<T>`, `RpcError` — is now
  `internal`; it was never meant to be part of the public surface. Thrown exceptions (`RpcException`,
  `TransactionFailedException`) and the streaming `RpcContextValue<T>` stay public.

## [0.3.0]

### Added

- More cluster reads: `GetVoteAccountsAsync`, `GetInflationRewardAsync`, `GetLeaderScheduleAsync`,
  `GetBlocksAsync`, and `GetClusterNodesAsync`.
- A `jsonParsed` account path: `GetParsedAccountInfoAsync` and `SubscribeParsedAccountAsync`
  (`accountSubscribe`), decoding a recognized account to a typed `Parsed` view and falling back to raw bytes
  when the owning program is unknown.

## [0.2.0]

### Added

- **`jsonParsed` read path** — `SolanaRpcClient.GetParsedTransactionAsync` and `GetParsedBlockAsync`,
  plus `SolanaWsClient.SubscribeParsedBlocksAsync`, returning the node's decoded instructions, token
  balances and logs without local Borsh work. Every instruction keeps both its typed `Parsed` form
  and its raw `ProgramId` / `Accounts` / `Data`, so nothing is dropped. New models live in
  `SolSharp.Rpc.Models.Parsed`.
- `TokenBalance.ProgramId` — the token program (SPL Token or Token-2022) that owns the account, on
  transaction-meta token balances.
- `<seealso>` links from every RPC model to its Solana documentation page (jump from IntelliSense).

## [0.1.0]

Initial stable release: a lean, modern .NET 8 Solana SDK shipped as a single `SolSharp` package that
bundles four layered assemblies.

### Added

- **Core** — `PublicKey` value type, base58, compact-u16 (shortvec), Borsh reader/writer,
  `Commitment`, and well-known program / sysvar / mint constants.
- **Wallet** — Ed25519 `Keypair` (generate, parse, sign), signature verification, on-curve check.
- **Rpc** — typed JSON-RPC reads (accounts, transactions, blocks, cluster state), `getTransaction`
  rich metadata with a typed `TransactionError`, SPL `Mint` / `TokenAccount` decoders, multiplexed
  WebSocket streaming with auto-reconnect, dependency-injection registration with a resilience
  pipeline, and send / simulate / confirm.
- **Programs** — System / Token (+ Token-2022) / Associated Token Account / Compute Budget / Memo /
  Address Lookup Table instruction builders, PDA & ATA derivation, legacy and v0 (versioned)
  transaction building, signing and serialization, `Transaction.Deserialize`, and instruction
  decompilation — every wire format validated byte-for-byte against the Rust `solana-sdk`.

[Unreleased]: https://github.com/jecacs/SolSharp/compare/v3.2.0...HEAD
[3.2.0]: https://github.com/jecacs/SolSharp/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/jecacs/SolSharp/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/jecacs/SolSharp/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/jecacs/SolSharp/compare/v1.3.0...v2.0.0
[1.3.0]: https://github.com/jecacs/SolSharp/releases/tag/v1.3.0
[1.2.0]: https://github.com/jecacs/SolSharp/releases/tag/v1.2.0
[1.1.0]: https://github.com/jecacs/SolSharp/releases/tag/v1.1.0
[1.0.1]: https://github.com/jecacs/SolSharp/releases/tag/v1.0.1
[1.0.0]: https://github.com/jecacs/SolSharp/releases/tag/v1.0.0
[0.7.0]: https://github.com/jecacs/SolSharp/releases/tag/v1.0.0
[0.6.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.6.0
[0.5.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.5.0
[0.4.1]: https://github.com/jecacs/SolSharp/releases/tag/v0.4.1
[0.4.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.4.0
[0.3.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.3.0
[0.2.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.2.0
[0.1.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.1.0
