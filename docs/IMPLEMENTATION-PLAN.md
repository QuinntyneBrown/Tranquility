# Tranquility Implementation Plan — Solution Skeleton + Vertical Slice

Status: Approved
Date: 2026-07-15
Scope source: `docs/specs/` requirements baseline (87 requirements: 20 L1 / 67 L2, 15 subsystems)

---

## 1. Objective

Implement a full .NET solution skeleton plus a working vertical slice:

> UDP ingest → CCSDS space packet decommutation → XTCE-driven parameter processing and alarms → HTTP + WebSocket API.

Remaining subsystems are stubbed as compiled projects with interface-level types tracing to L2 requirement IDs, to be implemented in later phases.

## 2. Constraints carried from the specifications

| Constraint | Source |
|---|---|
| Clean-room: implement only from `docs/specs/` (esp. `TRQ-ICD-API.md`) and published CCSDS/XTCE standards. Never consult reference-system source code. | TRQ-SCOPE.md, TRQ-ICD-API.md §6 |
| API implemented from the prose-derived in-repo ICD only. JSON wire format only; protobuf subprotocol deferred until the TBD-012 legal question is decided. | TRQ-OPEN-QUESTIONS.md (TBD-012) |
| .NET current LTS (net10.0), C#, Linux-first deployment. | L2-QLT-001 |
| Clean Architecture layering; CQRS for the command/query split. | L2-QLT-006 |
| Decommutation core is pure functions over byte spans with no I/O dependencies. | L2-QLT-002, L2-SPP-003 |
| Apache-2.0 license; dependency set license-audited in CI. | L2-QLT-003 |
| No performance numbers asserted; benchmark hooks only. Targets are TBD. | TBD-013 |
| WebSocket transport: native `System.Net.WebSockets`. SignalR imposes its own framing, incompatible with the ICD's `/api/websocket` + json subprotocol. ADR-0002 moves OPEN → PROPOSED with this rationale. | ADR-0002, TRQ-ICD-API.md |

## 3. Solution structure

```
Tranquility.slnx
Directory.Build.props            # net10.0, nullable, warnings-as-errors, Apache-2.0 metadata
LICENSE                          # Apache-2.0
README.md                        # quickstart + requirement traceability pointers
.github/workflows/ci.yml         # build + test + license audit (Linux runner)
src/
  Tranquility.Core/              # Domain. Pure, zero I/O dependencies:
                                 #   Ccsds/          SPP header, TM frames, time codes
                                 #   Mdb/            parameter/type/container model
                                 #   Decommutation/  container match, extraction, calibration
                                 #   Alarms/         limit evaluation
  Tranquility.Application/       # CQRS commands/queries, processor pipeline,
                                 # ports (ILink, IMdbSource, IParameterSink)
  Tranquility.Infrastructure/    # XTCE XML loader, UDP link, in-memory stores, audit sink
  Tranquility.Server/            # ASP.NET Core host: HTTP API + /api/websocket
  Tranquility.Commanding/        # stub (interface-level only)
  Tranquility.Cfdp/              # stub (interface-level only)
  Tranquility.Archive/           # stub (interface-level only)
tools/
  Tranquility.DiffHarness/       # differential conformance harness skeleton (DIF)
  Tranquility.PacketGen/         # demo packet sender for the vertical slice
tests/
  Tranquility.Core.Tests/        # golden-vector unit tests (deterministic)
  Tranquility.Application.Tests/
  Tranquility.Server.Tests/      # WebApplicationFactory integration + WebSocket tests
```

## 4. Vertical slice definition

UDP link receives CCSDS space packets → SPP header parse → container matching against XTCE-loaded MDB → parameter extraction + calibration → limit/alarm evaluation → parameter cache → HTTP API reads + WebSocket push.

Subsystem coverage in this pass:

| Subsystem | Coverage |
|---|---|
| LNK, SPP, MDB, PAR, API, RTS | Implemented in slice |
| TIM | Time-code decode (CUC, CDS) in Core |
| SDL | TM frame decode + M_PDU extraction in Core with tests; running slice ingests packet-level UDP |
| LIF | Single instance/processor lifecycle |
| QLT | Build props, CI, license audit, test strategy |
| SEC | Skeleton only: authN placeholder, authorization hooks on privileged paths, audit abstraction |
| CMD, FDP, ARC, DIF | Stub projects with interfaces tracing to L2 IDs |

## 5. Work breakdown

| # | Task | Content |
|---|---|---|
| 1 | repo-scaffold | Solution, projects, `Directory.Build.props`, LICENSE, `.gitignore`, `.editorconfig`, README skeleton |
| 2 | ci-pipeline | GitHub Actions on ubuntu-latest: restore, build, test, NuGet license audit (L2-QLT-003) |
| 3 | core-spp | CCSDS 133.0-B space packet primary/secondary header parse, pure span-based, golden-vector tests |
| 4 | core-time | CCSDS 301.0-B CUC + CDS decode to UTC, configurable epoch, golden-vector tests |
| 5 | core-tm-frames | CCSDS 132.0-B TM transfer frame header parse + M_PDU first-header-pointer packet extraction |
| 6 | mdb-model | Core MDB model: SpaceSystem tree, parameter types (integer/float/enumerated), parameters, sequence containers with inheritance/restriction, qualified-name lookup |
| 7 | mdb-xtce-loader | Infrastructure XTCE XML loader for the supported subset; validation errors per L2-MDB requirements; sample fixtures + tests |
| 8 | core-decomm | Container matching, bit-level big-endian extraction over `ReadOnlySpan<byte>`, polynomial + enumeration calibration; pure, no I/O |
| 9 | params-processing | Parameter cache (latest values), static limit evaluation with severities, alarm state model |
| 10 | app-pipeline | Processor pipeline link → decomm → cache → subscription fan-out; CQRS handlers; `ILink` port + UDP packet-in link |
| 11 | api-http | Endpoints per `TRQ-ICD-API.md`: instances, processors, links, MDB parameters, parameter values; error envelope `{"exception":{"type","msg"}}`; RFC 3339 UTC timestamps |
| 12 | api-websocket | `/api/websocket`, json subprotocol, call/seq correlation, parameter subscription topic with push; native WebSockets |
| 13 | security-skeleton | AuthN placeholder, authorization policy hooks, audit log abstraction |
| 14 | subsystem-stubs | Commanding/Cfdp/Archive/DiffHarness projects; interface-level types tracing to L2 IDs |
| 15 | e2e-demo | Sample mission XTCE file, PacketGen tool, integration test asserting parameter values via HTTP and WebSocket end-to-end |
| 16 | docs-update | README quickstart, implementation-to-requirement-ID map, ADR-0002 → PROPOSED |

## 6. Dependency order

```
repo-scaffold ─┬→ ci-pipeline
               ├→ core-spp ──────────┐
               ├→ core-time          │
               ├→ core-tm-frames     │
               ├→ mdb-model ──┬──────┴→ core-decomm → params-processing ─┐
               │              └→ mdb-xtce-loader ────────────────────────┴→ app-pipeline ─┬→ api-http ──┬→ e2e-demo → docs-update
               ├→ security-skeleton                                                       └→ api-websocket ─┘
               └→ subsystem-stubs
```

Seven tasks run in parallel after scaffolding.

## 7. Verification strategy

- Every Core parser: unit tests with hand-computed golden vectors (Verification: Test, per TRQ-VCRM.md).
- End-to-end integration test: boot server, stream packets via PacketGen, assert parameter values over HTTP and WebSocket (Verification: Demonstration).
- CI green on Linux is the completion gate.

## 8. Explicit non-goals for this pass

- No protobuf anywhere (TBD-012 open — blocking legal question).
- No performance targets asserted (TBD-013 open).
- No COP-1/CLTU, CFDP, or archive behaviour — interfaces only.
- No identity provider integration — security skeleton only.
