# Rust SDK parity

SolSharp is an independently implemented C# client SDK. Its wire contracts are derived
from and verified against pinned Anza Solana SDK, Agave, and Solana Program Library
sources. The pins below make every compatibility statement reproducible; parity is not
inferred from documentation pages or from another .NET SDK.

This matrix covers functionality that belongs in an application-side SDK: keys and
signatures, address derivation, messages and transactions, instruction construction,
account decoding, JSON-RPC, and PubSub. Validator runtime, banking, consensus, gossip,
TPU, ledger storage, node administration, and command-line tools are deliberately out of
scope.

## Pinned upstream contracts

| Repository | Commit | Contract used by SolSharp |
| --- | --- | --- |
| [anza-xyz/solana-sdk](https://github.com/anza-xyz/solana-sdk) | `ec7a0467e268774b724d55120ad952b518f27d64` | addresses, hashes, signatures, messages, transactions, native program interfaces |
| [anza-xyz/agave](https://github.com/anza-xyz/agave) | `ab6553293094e59dee7d3e7c928c7fa1023d0684` | JSON-RPC/PubSub schemas, runtime sanitization, transaction-version feature gates |
| [solana-program/token](https://github.com/solana-program/token) | `dbd89438108fda6ac40866d1ccfbb85f2e7436d4` | classic SPL Token instructions and account layouts |
| [solana-program/token-2022](https://github.com/solana-program/token-2022) | `6d87d47d6bbde19a02521164edd72d246c5736a7` | Token-2022 base instructions, extensions, and interface crate pins |
| [solana-program/associated-token-account](https://github.com/solana-program/associated-token-account) | `5ef2d950ccdebb35a73c77e8008910cf15a87a5f` | associated-token-account instructions and derivation |
| [solana-program/address-lookup-table](https://github.com/solana-program/address-lookup-table) | `8ebd5f4964454bc6b86d86ff191702d33c52490b` | address lookup table instructions and state |
| [solana-program/system](https://github.com/solana-program/system) | `8c47b48e8e129ab195db3e3d2a8334dd8bbd94aa` | System Program instructions and generated client layouts |

All repositories above are Apache-2.0 licensed. SolSharp remains MIT licensed; attribution
and the exact source pins shipped with the package are recorded in
`THIRD_PARTY_NOTICES.md`.

## Status legend

- **Compatible** — the current public API covers the applicable pinned client contract and
  exact upstream vectors or equivalent cross-implementation vectors test its wire form.
- **Implemented** — the contract is present, but one or more explicitly named compatibility or
  packaged-runtime validation gates remain.
- **Partial** — useful support exists, but named client-side contracts remain missing.
- **In progress** — implementation is part of the current parity release.
- **Out of scope** — server/runtime behavior that a client SDK must not reimplement.

## Client parity matrix

| Domain | Status | SolSharp coverage | Remaining work for the parity release |
| --- | --- | --- | --- |
| Address/PublicKey | Compatible | 32-byte value semantics, base58, JSON, byte copying, curve check | None in the pinned client contract |
| Hash | Compatible | 32-byte typed hash, base58/JSON, typed blockhash overloads | None in the pinned client contract |
| Ed25519 keypair/signature | Compatible | generation; Rust/wallet byte, base58, and `id.json` import/export; strict signing and verification; typed 64-byte signatures; RFC/upstream vectors | None in the pinned client contract |
| BLS12-381 keypair/signature | Implemented | Pinned min-pk proof-of-possession scheme, typed compressed values, vote-account derivation/signing/PoP vectors, strict subgroup/infinity validation, PoP-gated same-message aggregation | Packaged runtime execution is proven on Linux x64; execute the same package smoke on the other four advertised native RIDs |
| Signed off-chain messages | Compatible | version-0 domain, restricted/limited/extended UTF-8 formats, bounded parse, hash, sign, verify, exact upstream vectors | None in the pinned client contract |
| BIP-39 and SLIP-0010 | Compatible | Solana CLI and hardened wallet derivation paths | None in the pinned client contract |
| PDA and seeded addresses | Compatible | create/find program address, `create_with_seed`, canonical seed limits | None in the pinned client contract |
| Core encodings | Compatible | base58, canonical compact-u16, bounded Borsh primitives/collections/strings | Extend only when a public account/instruction contract requires another type |
| Sysvars and feature gates | Compatible | all current sysvar IDs; bounded Clock, Rent, EpochSchedule, EpochRewards, LastRestartSlot, SlotHashes, SlotHistory, StakeHistory and Instructions data; Feature activate/revoke clients | None in the pinned client contract |
| Legacy message | Compatible | compile, sanitize, serialize/deserialize, decompile | None |
| Message v0 + ALT | Compatible | compile, lookup extraction, sanitize, serialize/deserialize, decompile; context-slot address visibility and conservative deactivation usability | None |
| SIMD-0385 message v1 | Compatible | inline configuration, compile/sanitize/serialize/deserialize/decompile, exact pinned vectors and limits | Runtime activation remains cluster-specific |
| Transaction envelope | Compatible | version-routed legacy/v0/V1 serialization, typed signature slots, partial/full/external signing, per-slot verification, message hash, bounded allocation | None in the pinned client contract |
| System Program | Compatible | full current application-side instruction set, including nonce upgrade, seeded nonce creation, prefunded creation, and transfer-many composition | None in the pinned client contract |
| Compute Budget | Compatible | unit limit/price, heap frame, loaded-account-data limit | None |
| Address Lookup Table Program | Compatible | create, extend, freeze, deactivate, close; strict account decoding; SlotHashes-aware status, active-address prefix, and indexed lookup | None |
| Memo Program | Compatible | strict UTF-8 memo construction with signer metas | None |
| Stake and Vote | Compatible | current stable instruction families, composites, compact/tower and V2/BLS forms; bounded Stake and versioned Vote account state | None in the pinned client contract |
| Native loaders, feature gate, and precompiles | Compatible | legacy/upgradeable/V4 loader clients and states; feature activation/revocation; Ed25519/Secp256k1/Secp256r1 self-contained and offset-table clients | None in the pinned client contract |
| Classic SPL Token | Compatible | complete pinned instruction family, checked and multisig variants, fixed account/mint decoding | None in the pinned client contract |
| Associated Token Account | Compatible | create, idempotent create, recover nested, canonical derivation | None in the pinned client contract |
| Token-2022 base and non-confidential extensions | Compatible | base instructions, transfer fees, pointer/default-state/memo/CPI/interest/scaled/pausable/permissioned-burn extensions | None in the pinned client contract |
| Token metadata interface | Compatible | initialize, field update/removal, authority update, ranged emit | None in the pinned client contract |
| Token group and transfer-hook client helpers | Compatible | group/member instructions and state; validation PDA; extra-account-meta/seed codecs; bounded TLV decoding; async off-chain account resolution and de-escalation | None in the pinned client contract |
| Confidential Token-2022 client contracts | Compatible | confidential transfer/fee/mint-burn and permissioned confidential-burn instructions, raw POD proof locations, native proof-program and ElGamal-registry clients/state | ZK proof generation and ciphertext arithmetic are explicit cryptographic exclusions |
| Token/Token-2022 account decoding | Compatible | canonical base Mint/TokenAccount/multisig state, typed Token-2022 TLV/extensions, metadata/group/hook/registry state, and instruction decoders | None in the pinned client contract |
| HTTP JSON-RPC | Compatible | Every non-admin, non-obsolete method in the pinned `RpcRequest` surface; exact account-data encoding union and effective context/filter/slice/detail config variants; bounded responses, batching, typed errors, explicit V1 raw/parsed opt-ins and parsed V1 configuration | None in the pinned client contract |
| WebSocket PubSub | Compatible | Full pinned subscription families; `SubscribeAccountWithOptionsAsync` / `SubscribeProgramWithOptionsAsync` expose effective configs and the exact legacy binary/base58/base64/jsonParsed-fallback/base64+zstd union; bounded multiplexing, cancellation isolation, reconnect/replay, parsed program state, early signature receipt, and explicit V1 block opt-ins | None in the pinned client contract |
| Native AOT/trimming | Compatible | source-generated JSON, AOT annotations, package-consumer smoke app | None for managed paths; BLS native-RID execution is tracked separately above |

For `accountSubscribe`, pinned Agave applies only encoding and commitment to notifications;
`programSubscribe` also applies filters. Both exact SolSharp methods return the closed `RpcAccountData` union:
bare legacy `binary`, tagged base58/base64/base64+zstd, or `jsonParsed`, whose unknown-program branch is a
tagged base64 tuple. The pinned PubSub config structs also contain account/program `dataSlice` and
`minContextSlot` (plus program `withContext`/`sortResults`), but Agave's subscription encoder does not apply
them. SolSharp deliberately does not publish those no-op WebSocket knobs; the corresponding HTTP options are
effective and are exposed.

## Verification requirements

A row may be promoted to **Compatible** only when all applicable checks pass:

1. The implementation is compared with the pinned Rust source, including account order,
   signer/writable flags, discriminators, integer widths, optional-value encoding, limits,
   and sanitization behavior.
2. Money-critical wire data has an exact known-answer test from upstream or an independently
   generated Rust-compatible vector; a C# encode/decode round trip alone is insufficient.
3. Malformed input and boundary behavior are tested where the Rust implementation rejects it.
4. Every public API has XML documentation and follows the repository's nested-fixture test
   convention.
5. The complete solution builds with warnings as errors, all offline tests pass, formatting and
   diff checks are clean, package validation succeeds, and the AOT smoke app consumes the packed
   NuGet artifact.

## Explicit exclusions

The following Rust components are not SDK parity targets: validator/runtime execution,
Bank and AccountsDB, consensus/fork choice, gossip, Turbine/repair, TPU/QUIC services,
ledger/blockstore, snapshot creation, RPC server implementation, node-admin/deprecated
storage RPC methods, CLI binaries, test validators, and program processor code. SolSharp
constructs and decodes their public client contracts; it does not reproduce the Solana node.

Token-2022 zero-knowledge proof generation/verification and ElGamal ciphertext arithmetic are
also outside the current managed client boundary. SolSharp constructs their exact instruction,
POD, proof-location, and account-state contracts from caller-supplied cryptographic material; it
does not substitute a home-grown proof system. SIMD-0385 V1 support likewise describes the wire
contract only: activation remains a cluster feature gate controlled by validators.

BLS same-message aggregation is exposed only through proof-of-possession-verified public-key wrappers.
The pinned SDK's distinct-message screening path is consensus-oriented and deliberately is not presented
as a general application-security primitive by SolSharp.

Deprecated compatibility artifacts are not promoted as current application APIs: the Stake
`Redelegate` variant was deprecated before activation and has no builder, while the legacy Fees,
Rewards, and RecentBlockhashes sysvar addresses remain available for wire identification without new
typed state decoders. Current Stake operations and nondeprecated sysvar states are covered above.
