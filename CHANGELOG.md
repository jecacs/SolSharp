# Changelog

All notable changes to SolSharp are documented here. The format is loosely based on
[Keep a Changelog](https://keepachangelog.com), and the project follows
[semantic versioning](https://semver.org) — from 1.0.0 breaking changes only come with a major
version (on the earlier 0.x releases, minor versions could carry them).

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

[0.4.1]: https://github.com/jecacs/SolSharp/releases/tag/v0.4.1
[0.4.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.4.0
[0.3.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.3.0
[0.2.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.2.0
[0.1.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.1.0
