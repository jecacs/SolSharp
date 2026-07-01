# Changelog

All notable changes to SolSharp are documented here. The format is loosely based on
[Keep a Changelog](https://keepachangelog.com), and the project follows
[semantic versioning](https://semver.org) — while on 0.x, minor releases may carry breaking changes.

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
