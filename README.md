# Tranquility

A mission control / C3 (Command, Control, Communication) server implemented in
modern .NET, designed to pair with browser-based telemetry visualization
clients over a documented HTTP + WebSocket API.

Tranquility is a **clean-room implementation** derived exclusively from
published standards (CCSDS, OMG XTCE) and published API documentation. See
`docs/specs/` for the requirements baseline and provenance rules.

## Status

Complete end-to-end implementation, built test-first (ATDD). Every one of the
67 L2 requirements is covered by a traced acceptance test
(`[Requirement("L2-…")]`); the traceability ratchet and a final coverage gate
enforce that coverage only moves forward. The verification cross-reference
matrix is generated from the suite: `docs/specs/TRQ-VCRM.md`.

Subsystems implemented: space data-link framing (TM/AOS/USLP/TC), space-packet
decommutation, XTCE mission database, parameter processing and alarms,
real-time streaming (json + clean-room protobuf WebSocket subprotocols),
SQLite archive with replay, instance/processor lifecycle, data links,
commanding (issue/queue/history, COP-1, CLTU, TC frames), time correlation,
CFDP file transfer, security (IAM, authZ, TLS, hash-chained audit), the
differential conformance harness, and the quality/governance gates.

## Layout

| Path | Content |
|---|---|
| `src/Tranquility.Core` | Pure, deterministic domain: CCSDS parsing, MDB model, decommutation, alarms, COP-1/CLTU, CFDP, time correlation. Zero I/O, zero clock, zero package refs. |
| `src/Tranquility.Application` | CQRS handlers, ports, processor pipeline, commanding/CFDP/TCO runtimes. |
| `src/Tranquility.Infrastructure` | SQLite archive/IAM/audit, XTCE loader, UDP/loopback links, filestore. |
| `src/Tranquility.Wire` | Clean-room `.proto` schema, JSON DTOs, RFC 3339 converters, exception envelope. |
| `src/Tranquility.Server` | ASP.NET Core host: HTTP API + `/api/websocket`, JWT auth, TLS. |
| `tools/Tranquility.DiffHarness` | Differential conformance harness (external-observation only). |
| `tests/Tranquility.AcceptanceTests` | The traced ATDD suite + architecture/determinism/metadata gates. |
| `tests/Tranquility.Benchmarks` | Performance verification against `thresholds.json`. |
| `docs/specs/` | Requirements (L1/L2 per subsystem), ICD, CFDP profile, VCRM. |
| `docs/detailed-designs/` | 116 PlantUML sequence diagrams, one per behaviour. |
| `docs/adr/` | Architecture decisions (all resolved). |

## Build and test

```sh
dotnet build
dotnet test
```

The acceptance suite boots the full pipeline two ways: in-process
(`WebApplicationFactory`) and over real TLS Kestrel with genuine wss and UDP
sockets. `DEVELOPMENT.md` documents the RED→GREEN milestone convention.

## Quickstart

```sh
dotnet run --project src/Tranquility.Server
```

Then over HTTP/WebSocket (see `docs/specs/application-programming-interface/`):

```sh
curl http://localhost:5208/api/instances
curl http://localhost:5208/api/mdb/sim
# WebSocket /api/websocket, subprotocol json:
#   {"type":"parameters","id":1,"options":{"instance":"sim","id":[{"name":"/SampleSat/Temperature"}]}}
```

## Requirement traceability

Every acceptance test carries `[Requirement("L2-…")]`; `docs/specs/TRQ-VCRM.md`
is regenerated from those traits and lists the verifying test(s) per
requirement. Coverage is enforced by meta-tests in
`tests/Tranquility.AcceptanceTests/Traceability/`.

## Performance

Declared targets in `docs/PERFORMANCE-BASELINE.md`, verified by
`tests/Tranquility.Benchmarks` and the `PerfSmoke` acceptance tests (ADR-0005).

## License

Apache-2.0. Dependencies are license-audited in CI (L2-QLT-003).
