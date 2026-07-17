# Tranquility

A mission control / C3 (Command, Control, Communication) server implemented in modern .NET, designed to pair with browser-based telemetry visualization clients over a documented HTTP + WebSocket API.

Tranquility is a **clean-room implementation** derived exclusively from published standards (CCSDS, OMG XTCE) and published API documentation. See `docs/specs/` for the requirements baseline and provenance rules.

## Status

Skeleton + vertical slice: UDP packet ingest → CCSDS space packet decommutation → XTCE-driven parameter processing and alarms → HTTP + WebSocket API.

See `docs/specs/README.md` for the subsystem requirement index.

## Layout

| Path | Content |
|---|---|
| `src/Tranquility.Core` | Pure domain: CCSDS parsing, MDB model, decommutation, alarms. No I/O dependencies. |
| `src/Tranquility.Application` | CQRS handlers, processor pipeline, ports. |
| `src/Tranquility.Infrastructure` | XTCE loader, UDP link, in-memory stores. |
| `src/Tranquility.Server` | ASP.NET Core host: HTTP API + `/api/websocket`. |
| `src/Tranquility.{Commanding,Cfdp,Archive}` | Interface-level stubs for later phases. |
| `tools/Tranquility.PacketGen` | Demo CCSDS packet sender. |
| `tools/Tranquility.DiffHarness` | Differential conformance harness skeleton. |
| `tests/` | Unit + integration tests. |
| `docs/specs/` | Requirements categorized by subsystem and split into L1/L2 artifacts. |

## Build and test

```sh
dotnet build
dotnet test
```

## Quickstart: run the vertical slice

Terminal 1 — start the server (loads `samples/SampleSat.xml`, listens for
CCSDS space packets on UDP 10015):

```sh
dotnet run --project src/Tranquility.Server
```

Terminal 2 — stream sample telemetry:

```sh
dotnet run --project tools/Tranquility.PacketGen -- 127.0.0.1 10015 20 500
```

Query current values over HTTP (port from server output):

```sh
curl http://localhost:5208/api/instances
curl http://localhost:5208/api/links/sample
curl http://localhost:5208/api/processors/sample/realtime/parameters/SampleSat/Temperature
```

Or subscribe over WebSocket at `/api/websocket` (subprotocol `json`):

```json
{"type":"parameters","id":1,"options":{"id":[{"name":"/SampleSat/Temperature"}]}}
```

Configuration keys: `Tranquility:Instance` (default `sample`),
`Tranquility:MdbPath` (default `SampleSat.xml` beside the binary),
`Tranquility:UdpPort` (default `10015`).

## Requirement traceability (implemented slice)

Code carries `Implements:`/`Traces:` doc comments naming L2 requirement IDs.
Where each implemented requirement lives:

| Requirement | Implementation | Verified by |
|---|---|---|
| L2-SPP-001/002 (space packet header) | `Core/Ccsds/SpacePacket.cs` | `Core.Tests/Ccsds/SpacePacketHeaderTests` |
| L2-SDL (TM frame, M_PDU extract) | `Core/Ccsds/TmFrameHeader.cs`, `VirtualChannelPacketExtractor.cs` | `Core.Tests/Ccsds/TmFrameTests` |
| L2-TIM (CUC/CDS time codes) | `Core/Ccsds/CucTimeCodec.cs`, `CdsTimeCodec.cs` | `Core.Tests/Ccsds/TimeCodecTests` |
| L2-MDB-001/002 (MDB model, XTCE load) | `Core/Mdb/*`, `Infrastructure/Xtce/XtceLoader.cs` | `Application.Tests/XtceLoaderTests` |
| L2-SPP-003, L2-PAR-001/002 (decomm, calibration, limits) | `Core/Decommutation/*`, `Core/Alarms/*` | `Core.Tests/Decommutation/*` |
| L2-LNK-001/002 (links) | `Infrastructure/Links/UdpPacketLink.cs`, link endpoints | `Server.Tests/ApiIntegrationTests` |
| L2-API-001/002/004 (HTTP resources, error envelope, RFC 3339) | `Server/Program.cs`, `Server/Api/*` | `Server.Tests/ApiIntegrationTests` |
| L2-API-003, L2-RTS-001..003 (WebSocket subscribe/push) | `Server/WebSockets/WebSocketApiHandler.cs` | `Server.Tests/EndToEndTests` |
| L2-LIF-001/002 (instance/processor lifecycle) | `Application/InstanceRegistry.cs`, `Server/Hosting/TelemetryHostedService.cs` | `Server.Tests` |
| L2-SEC (skeleton: authZ hook, audit) | `Server/Security/SecuritySkeleton.cs`, `Application/Abstractions/Ports.cs` (IAuditLog) | `Server.Tests` |
| L2-QLT-001/003 (Linux CI, license audit) | `.github/workflows/ci.yml` | CI run |
| L2-CMD, L2-FDP, L2-ARC, L2-DIF (stubs) | `Commanding/Cfdp/Archive` `Contracts.cs`, `tools/Tranquility.DiffHarness` | later phase |

Full requirement text: `docs/specs/<subsystem>/L1.md` and `L2.md`.

## License

Apache-2.0. Dependencies are license-audited in CI (L2-QLT-003).
