# Security Policy

SolSharp handles private keys and builds transactions that move funds, so security reports are
taken seriously and are appreciated.

## Supported versions

| Version | Supported |
| ------- | --------- |
| 2.x     | ✅        |
| 1.x     | ✅        |
| < 1.0   | ❌        |

Fixes ship as patch releases of the latest supported 2.x and 1.x lines.

## Reporting a vulnerability

**Please do not open a public issue for a vulnerability.**

Use GitHub's private vulnerability reporting: **[Security → Report a vulnerability](https://github.com/jecacs/SolSharp/security/advisories/new)**.
If that is not an option, email the maintainer via the address on the
[NuGet package page](https://www.nuget.org/packages/SolSharp) ("Contact owners").

What helps: the affected version, a minimal reproduction, and your assessment of the impact
(e.g. key material exposure, signature forgery, wire-format corruption, denial of service).

You can expect an acknowledgment within a few days. Please allow a fix and a patched release
before public disclosure; you will be credited in the advisory and the changelog unless you
prefer otherwise.

## Automated assurance

- Pull requests and `main` are checked against direct and transitive NuGet advisories; low through
  critical findings fail the security gate.
- Dependency Review rejects pull requests that introduce a known vulnerable dependency, and the
  advisory audit also runs weekly so newly published advisories are detected without a new commit.
- Ordinary CI/release restores are locked to committed NuGet dependency graphs. Five deterministic
  property-based tests exercise 5,000 bounded arbitrary, truncated, and single-byte-overwrite transaction
  cases per CI run.
- CodeQL runs the extended C# security query suite. OpenSSF Scorecard reports repository supply-chain
  posture, and the release workflow attaches the verified package, SHA-256 digest, and Sigstore/SLSA build
  provenance after the exact packed artifact passes the Native AOT smoke test.

These checks report known issues in the inputs and databases available at run time. A green badge is
useful evidence, but it is not proof that SolSharp is vulnerability-free and is not a professional audit.

## Scope notes

- SolSharp has **not** been professionally audited — treat it accordingly and simulate before
  sending value.
- The Ed25519 engine is [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography);
  vulnerabilities in BouncyCastle itself should be reported upstream, but a SolSharp report is
  still welcome so the dependency can be bumped quickly.
- BLS12-381 operations use [Nethermind.Crypto.Bls](https://www.nuget.org/packages/Nethermind.Crypto.Bls)
  and its packaged `blst` native backend. Backend or RID-specific failures are in scope for SolSharp;
  upstream cryptographic vulnerabilities should also be reported to the dependency maintainer.
- Key handling promises that *are* in scope: `Keypair` zeroes its secret on dispose/finalization,
  secrets never appear in logs or exception messages, and nothing in the library transmits key
  material anywhere.
