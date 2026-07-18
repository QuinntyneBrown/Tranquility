# Contributing to Tranquility

Thank you for helping improve Tranquility. Contributions may include code,
tests, specifications, protocol analysis, detailed designs, documentation,
issue triage, security review, performance evidence, and operational insight.

By participating, you agree to follow the
[Code of Conduct](CODE_OF_CONDUCT.md). Use [SUPPORT.md](SUPPORT.md) for help and
[SECURITY.md](SECURITY.md) for suspected vulnerabilities.

## Before contributing

- Search existing issues and pull requests before starting work.
- Open an issue before large features, protocol-profile changes, wire-contract
  changes, persistence migrations, or requirement-baseline changes.
- Never include mission data, credentials, signing keys, connection strings,
  private interface documents, or sensitive packet captures. Use synthetic
  fixtures and redact logs.
- Preserve the clean-room boundary. Do not consult or reproduce source code
  from the reference mission-control system.

## Development setup

```bash
git clone https://github.com/QuinntyneBrown/Tranquility.git
cd Tranquility

dotnet restore
dotnet build
dotnet test
```

The repository pins its .NET SDK in [global.json](global.json). See the
[README](README.md) for runtime configuration and
[DEVELOPMENT.md](DEVELOPMENT.md) for the complete ATDD convention.

## Development workflow

1. Create a branch from the latest `main` using `feat/`, `fix/`, `docs/`,
   or `chore/` followed by a short description.
2. Keep the change focused and avoid unrelated formatting or refactoring.
3. For behavior changes, work from a requirement and acceptance test:
   establish the failing behavior, implement the smallest correct change, then
   refactor while green.
4. Add or update `[Requirement("L2-…")]` coverage when behavior or requirement
   scope changes.
5. Update the relevant subsystem specification, detailed design, architecture
   decision, performance baseline, and changelog when their contract changes.
6. Open a pull request and respond to review feedback. Do not commit directly
   to `main`.

## Quality gates

Run checks appropriate to the change:

```bash
dotnet build --configuration Release
dotnet test --configuration Release

# Required when deterministic throughput paths change
dotnet test --configuration Release --filter "Category=PerfSmoke"

# Full benchmark when performance declarations change
dotnet run --project tests/Tranquility.Benchmarks --configuration Release -- \
  tests/fixtures/xtce/SampleSat.xml
```

Documentation-only and repository-metadata changes do not require runtime tests,
but all local Markdown links and referenced repository paths must resolve.

## Requirements and specifications

- Requirements live in `docs/specs/<full-subsystem-name>/L1.md` and `L2.md`.
- Requirement IDs are stable traceability keys; do not renumber them casually.
- Every L2 ID must remain covered by a passing acceptance test.
- Update `docs/specs/TRQ-VCRM.md` when verification mappings change.
- Keep subsystem folder names aligned between `docs/specs` and
  `docs/detailed-designs`.
- Record externally meaningful protocol or architecture choices under
  `docs/adr`.

## Architecture boundaries

- `Tranquility.Core` is deterministic and has no runtime I/O, clock reads, or
  package references.
- `Tranquility.Application` owns command/query paths and depends on ports, not
  infrastructure implementations.
- `Tranquility.Infrastructure` implements persistence, links, XTCE loading,
  IAM, audit, and filestore adapters.
- `Tranquility.Wire` owns JSON and protobuf wire contracts.
- `Tranquility.Server` is the composition root and transport host.
- Protocol and authorization behavior is server-authoritative; clients cannot
  bypass command, queue, lifecycle, or privilege rules.

## Commits and pull requests

Use concise imperative commit subjects, for example:

- `commanding: reject invalid queue transitions`
- `docs: clarify CFDP profile traceability`

A pull request should explain the problem and resulting behavior, link related
requirements and issues, include test evidence, and call out security,
compatibility, migration, performance, and rollback implications.

At least one maintainer approval is required. Merged human contributions are
recognized in [CONTRIBUTORS.md](CONTRIBUTORS.md); notable changes belong under
`Unreleased` in [CHANGELOG.md](CHANGELOG.md).
