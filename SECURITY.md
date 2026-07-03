# Security Policy

SolSharp handles private keys and builds transactions that move funds, so security reports are
taken seriously and are appreciated.

## Supported versions

| Version | Supported |
| ------- | --------- |
| 1.x     | ✅        |
| < 1.0   | ❌        |

Fixes ship as a patch release of the latest 1.x version.

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

## Scope notes

- SolSharp has **not** been professionally audited — treat it accordingly and simulate before
  sending value.
- The Ed25519 engine is [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography);
  vulnerabilities in BouncyCastle itself should be reported upstream, but a SolSharp report is
  still welcome so the dependency can be bumped quickly.
- Key handling promises that *are* in scope: `Keypair` zeroes its secret on dispose/finalization,
  secrets never appear in logs or exception messages, and nothing in the library transmits key
  material anywhere.
