# Changelog

All notable changes to SolSharp are documented here. The format is loosely based on
[Keep a Changelog](https://keepachangelog.com), and the project follows
[semantic versioning](https://semver.org) — while on 0.x, minor releases may carry breaking changes.

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

[0.4.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.4.0
[0.3.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.3.0
[0.2.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.2.0
[0.1.0]: https://github.com/jecacs/SolSharp/releases/tag/v0.1.0
