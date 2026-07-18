# Tranquility

A clean-room mission control and C3 server for telemetry, commanding, archive,
file transfer, and operational client integration.

[![CI](https://github.com/QuinntyneBrown/Tranquility/actions/workflows/ci.yml/badge.svg)](https://github.com/QuinntyneBrown/Tranquility/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-c6fb50.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C%23](https://img.shields.io/badge/C%23-latest-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Requirements](https://img.shields.io/badge/L2_requirements-67%2F67-1d8fb9.svg)](docs/specs/TRQ-VCRM.md)
[![Contributions welcome](https://img.shields.io/badge/contributions-welcome-1d8fb9.svg)](CONTRIBUTING.md)

[Specifications](docs/specs/README.md) |
[Detailed designs](docs/detailed-designs/README.md) |
[Architecture decisions](docs/adr/README.md) |
[Development](DEVELOPMENT.md) |
[Contributing](CONTRIBUTING.md)

## About the project

Tranquility is a long-lived, stateful backend for mission telemetry ingest,
CCSDS packet and frame processing, XTCE mission databases, parameter
evaluation, commanding, archive and replay, file transfer, time correlation,
security, and HTTP/WebSocket client access.

The implementation is clean-room: behavior is derived from published CCSDS and
OMG XTCE standards, published API documentation, and the requirements under
[docs/specs](docs/specs/README.md). It does not depend on source code from the
reference mission-control system.

The differentiator is end-to-end traceability. Every detailed L2 requirement
is bound to one or more acceptance tests through a `[Requirement("L2-…")]`
attribute. Meta-tests prevent requirements from disappearing silently, and the
[verification cross-reference matrix](docs/specs/TRQ-VCRM.md) records the
executable evidence.

Tranquility has not completed an independent mission-safety, security, privacy,
or operational-readiness assessment. Treat it as an engineering baseline, not
a certified flight-operations system.

## Features

- TM, AOS, USLP, and TC transfer-frame handling with explicit diagnostics
- CCSDS space-packet parsing and deterministic XTCE-driven decommutation
- Mission database loading, validation, aliases, commands, and metadata queries
- Parameter calibration, limits, alarms, derived values, and update publication
- Command issue, queues, history, verification, COP-1, CLTU, and TC framing
- CFDP file-transfer lifecycle with a declared conformance profile
- SQLite-backed archive, replay, IAM, and hash-chained audit records
- Real-time JSON and clean-room protobuf WebSocket subprotocols
- Instance, processor, link, replay, and time-correlation lifecycle operations
- JWT authentication, privilege authorization, TLS verification, and IAM APIs
- Differential-conformance tooling based only on external observations
- Linux-first CI, dependency-license enforcement, and performance gates

## Getting started

### Prerequisites

- [.NET SDK 10.0.100 or compatible 10.0 feature band](https://dotnet.microsoft.com/)
  (pinned in [global.json](global.json))
- Git
- Optional: Java and [PlantUML](https://plantuml.com/) to render detailed designs

### Local development

```bash
git clone https://github.com/QuinntyneBrown/Tranquility.git
cd Tranquility

dotnet restore
dotnet run --project src/Tranquility.Server --urls http://localhost:5208
```

The server reports its bound address at startup. A bare local launch uses an
ephemeral JWT signing key and has no seeded users or mission instances. Public
read surfaces can still be inspected, for example:

```bash
curl http://localhost:5208/api/instances
```

Configure `Tranquility:Security`, `Tranquility:Instances`,
`Tranquility:MdbDirectory`, and `Tranquility:DataDirectory` through normal
ASP.NET Core configuration providers before exercising authenticated mutation
paths or retaining operational data. Never commit signing keys, password
hashes derived from real credentials, mission data, or deployment secrets.

## Technology

| Area | Technologies |
|---|---|
| Runtime | .NET 10, ASP.NET Core, C# |
| Domain | Deterministic CCSDS, CFDP, COP-1, CLTU, XTCE, and time-code engines |
| Application | Explicit command/query dispatch, ports, processors, subscriptions |
| Persistence | SQLite archives, identity store, and audit chain |
| Wire protocols | HTTP/JSON, WebSocket JSON, clean-room protobuf |
| Quality | xUnit v3, CsCheck, traced ATDD, architecture and scope gates |
| Documentation | Markdown, PlantUML sequence designs, architecture decisions |
| Delivery | GitHub Actions on Linux and Windows, license and performance gates |

## Testing

```bash
dotnet build --configuration Release
dotnet test --configuration Release

# Deterministic performance smoke gate
dotnet test --configuration Release --filter "Category=PerfSmoke"

# Full standalone benchmark
dotnet run --project tests/Tranquility.Benchmarks --configuration Release -- \
  tests/fixtures/xtce/SampleSat.xml
```

The acceptance suite runs both in process and over real Kestrel TLS with
genuine WSS and UDP sockets. See [DEVELOPMENT.md](DEVELOPMENT.md) for the ATDD
RED → GREEN convention, traceability ratchet, determinism rules, and test
taxonomy.

## Project structure

```text
src/
  Tranquility.Core/             Pure protocol and mission-domain engines
  Tranquility.Application/      CQRS paths, ports, processors, runtime services
  Tranquility.Infrastructure/   SQLite, XTCE, links, IAM, audit, and filestore
  Tranquility.Wire/             JSON DTOs, converters, and protobuf contracts
  Tranquility.Server/           ASP.NET Core HTTP and WebSocket host
tests/
  Tranquility.AcceptanceTests/  Traced ATDD and conformance gates
  Tranquility.Benchmarks/       Declared performance verification
tools/
  Tranquility.DiffHarness/      External-observation conformance comparison
docs/
  specs/                        L1/L2 requirements and traceability matrix
  detailed-designs/             PlantUML behavior designs by subsystem
  adr/                          Accepted architecture decisions
```

## Documentation

| Document | Purpose |
|---|---|
| [Specification index](docs/specs/README.md) | L1 and L2 requirements for all 15 subsystems |
| [Verification matrix](docs/specs/TRQ-VCRM.md) | Requirement-to-acceptance-test evidence |
| [Detailed-design index](docs/detailed-designs/README.md) | 116 requirement-linked PlantUML behavior diagrams |
| [Architecture decisions](docs/adr/README.md) | Accepted implementation decisions and rationale |
| [Performance baseline](docs/PERFORMANCE-BASELINE.md) | Declared throughput and latency targets |
| [Development conventions](DEVELOPMENT.md) | ATDD workflow, traceability, determinism, and platform rules |

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) for the
test-first workflow, branch conventions, quality gates, specification rules,
and architecture boundaries. Participation is governed by the
[Code of Conduct](CODE_OF_CONDUCT.md).

Human contributors are recognized in [CONTRIBUTORS.md](CONTRIBUTORS.md), and
notable changes are recorded in [CHANGELOG.md](CHANGELOG.md).

## Security

Do not report vulnerabilities in a public issue. Follow
[SECURITY.md](SECURITY.md) to submit a private report. For usage questions and
non-sensitive defects, see [SUPPORT.md](SUPPORT.md).

## Governance

Maintainer responsibilities, decision making, and the path to becoming a
maintainer are described in [GOVERNANCE.md](GOVERNANCE.md).

## License

Copyright (c) 2026 Tranquility contributors. Released under the
[Apache License 2.0](LICENSE).
