# Third-party notices and compatibility sources

SolSharp is an independently written C# implementation. It does not bundle Rust binaries
or source code from the projects below. Their public wire formats, data layouts, validation
rules, and known-answer vectors are used as the authoritative compatibility contracts for
SolSharp.

The following repositories are licensed under the
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0):

| Project | Copyright holder/project | Source revision used for compatibility |
| --- | --- | --- |
| [Solana SDK](https://github.com/anza-xyz/solana-sdk) | Anza and Solana contributors | `ec7a0467e268774b724d55120ad952b518f27d64` |
| [Agave](https://github.com/anza-xyz/agave) | Anza and Solana contributors | `ab6553293094e59dee7d3e7c928c7fa1023d0684` |
| [SPL Token](https://github.com/solana-program/token) | Solana Program Library contributors | `dbd89438108fda6ac40866d1ccfbb85f2e7436d4` |
| [SPL Token-2022](https://github.com/solana-program/token-2022) | Solana Program Library contributors | `6d87d47d6bbde19a02521164edd72d246c5736a7` |
| [Associated Token Account](https://github.com/solana-program/associated-token-account) | Solana Program Library contributors | `5ef2d950ccdebb35a73c77e8008910cf15a87a5f` |
| [Address Lookup Table](https://github.com/solana-program/address-lookup-table) | Solana Program Library contributors | `8ebd5f4964454bc6b86d86ff191702d33c52490b` |
| [System Program](https://github.com/solana-program/system) | Solana Program Library contributors | `8c47b48e8e129ab195db3e3d2a8334dd8bbd94aa` |

The Token-2022 pin resolves the following interface contracts. Versions below come from
that checkout's `Cargo.lock` (rather than from a floating documentation page):

| Interface contract | Resolved version used for compatibility |
| --- | --- |
| `spl-token-interface` | `3.0.0` |
| `spl-token-2022-interface` | `3.1.1` |
| `spl-token-group-interface` | `0.7.2` |
| `spl-token-metadata-interface` | `1.0.1` |
| `spl-transfer-hook-interface` | `2.1.0` |
| `spl-tlv-account-resolution` | `0.11.1` |
| `solana-zk-elgamal-proof-interface` | `0.1.3` |
| `solana-zk-sdk-pod` | `0.1.2` |
| `spl-elgamal-registry-interface` | `0.2.1` |

The lock file also contains older interface versions used by ancillary workspace packages;
the table records the current contracts implemented by SolSharp. Exact package checksums
remain reproducible from the pinned checkout's lock file. Tests that quote a small upstream
byte vector identify the originating contract in their name or surrounding documentation.

## BLS runtime dependency

SolSharp's optional-in-use BLS12-381 key and signature API is backed by
[`Nethermind.Crypto.Bls` 1.0.5](https://www.nuget.org/packages/Nethermind.Crypto.Bls/1.0.5),
an MIT-licensed .NET binding at repository commit
[`a53533fc0112f16a453f744c39cb12cecf953784`](https://github.com/NethermindEth/blst-bindings/tree/a53533fc0112f16a453f744c39cb12cecf953784).
That package carries native [Supranational `blst`](https://github.com/supranational/blst)
binaries, copyright Supranational LLC, licensed under Apache-2.0. Its package SHA-256 is
`108f09b2210ac3e95a4610379fe3c58af26d01cc9f19927e748b8196aa5d88ac`.

Version 1.0.5 supplies native assets for Linux x64/arm64, macOS x64/arm64, and Windows x64.
It does not supply win-arm64, musl, mobile, or browser assets; applications that call the BLS
API therefore need one of the packaged native RIDs. The rest of SolSharp remains managed and
does not load `blst` unless a BLS operation is used.

SolSharp itself is licensed under the MIT License. Nothing in this notice implies endorsement
by Anza, the Solana Foundation, or the maintainers of the referenced projects. SolSharp is not
an official Anza or Solana Foundation product.
