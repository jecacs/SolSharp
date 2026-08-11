# Contributing to SolSharp

Thanks for helping improve SolSharp. Correctness and reviewability matter especially here because the
library handles private keys, signatures, transaction bytes, and network responses. Participation is subject
to the project's [Code of Conduct](CODE_OF_CONDUCT.md).

## Before opening an issue or pull request

- Search existing issues and pull requests first.
- Use a public issue for bugs, feature proposals, and compatibility questions.
- Do **not** disclose a suspected vulnerability in an issue. Follow [SECURITY.md](SECURITY.md) and use
  GitHub private vulnerability reporting.
- For a substantial API or wire-format change, open an issue before implementation so the contract and
  upstream reference can be agreed on first. Small fixes and documentation improvements may go directly
  to a pull request.

## Development setup

Install the **exact** .NET SDK pinned in `global.json`. The pin is not cosmetic: `IsAotCompatible` and
`PublishAot` make the SDK record its own toolchain packages in every `packages.lock.json`, so a different
SDK fails the locked restore below with `NU1004`.

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$(jq -r .sdk.version global.json)"
```

Then enable the repository's pre-push hook once per clone. It runs exactly the checks CI runs, so a failing
push is caught locally in about a minute instead of on the runner:

```bash
git config core.hooksPath .githooks
```

Rider and other JetBrains IDEs execute Git hooks on push by default (the push dialog has a "Run Git hooks"
checkbox). To bypass the hook for one push use `git push --no-verify`, or `SOLSHARP_SKIP_PREPUSH=1 git push`;
`SOLSHARP_PREPUSH_SKIP_TESTS=1` keeps every check except the test run.

Run these commands from the repository root — the hook runs the same list:

```bash
dotnet restore --locked-mode -p:NuGetAuditMode=all -warnaserror
dotnet build --no-restore --configuration Release -warnaserror
dotnet test --no-build --configuration Release --filter "TestCategory!=Integration"
dotnet format --no-restore --verify-no-changes --severity info
```

The offline test command excludes live mainnet/devnet probes. See [README.md](README.md#build--test) for
the integration-test endpoints and filters. The benchmark project is intentionally outside the solution;
changes to performance-sensitive code should also build and format it explicitly.

## Change requirements

- Keep each pull request focused. Explain the user-visible behavior, compatibility impact, and why the
  chosen design is preferable.
- Add deterministic tests for every behavior change. A public API is incomplete without coverage.
- For crypto, signing, program layouts, messages, or transactions, include an upstream known-answer vector
  or another independently generated compatibility vector. A local round trip alone is insufficient.
- Bound work and allocation before consuming lengths or collections from untrusted input. Never include
  private key material, mnemonics, tokens, complete hostile inputs, or other secrets in logs or exceptions.
- Preserve the dependency layering documented in [CLAUDE.md](CLAUDE.md): Core has no I/O or crypto engine;
  Wallet owns key and signature engines; Rpc owns transport; Programs owns Solana program and transaction
  contracts.
- Update XML documentation for public APIs. User-visible additions or changes also require the relevant
  usage guide, README, and changelog entries.
- Keep package versions and every affected `packages.lock.json` synchronized. Do not weaken locked restores,
  action SHA pins, workflow permissions, provenance checks, or package verification to make CI pass.

## Review and merge

All changes to `main` go through a pull request and the required CI, dependency, and code-scanning checks.
Security-sensitive, cryptographic, wire-format, and release-workflow changes require independent human
review when an eligible maintainer is available. Address review threads with code or a documented rationale;
do not resolve them merely to satisfy the merge gate.

By contributing, you agree that your contribution is licensed under the repository's [MIT License](LICENSE).
