# TRQ-SCOPE

## 1. Scope statement

Tranquility is a mission control / C3 server. It is a long-lived, stateful back-end for telemetry ingest, packet processing, commanding, archive/replay, and client API access. It is not a display client.

This baseline is clean-room and standards-traceable. It does not rely on reference mission-control system source code.

## 2. In scope (baseline)

| Subsystem | Description | Primary sources |
|---|---|---|
| API | Public HTTP + WebSocket compatibility surface | External API documentation package (URL redacted) |
| SDLP | CCSDS TM/AOS/USLP/TC frame handling | CCSDS 132.0-B, 732.0-B, 732.1-B, 232.0-B |
| SPP | Space Packet Protocol parsing and decommutation entry | CCSDS 133.0-B |
| MDB | XTCE mission database loading and model use | OMG XTCE 1.3 |
| PAR | Parameter processing, limits, alarms, derived values | OMG XTCE 1.3; reference mission-control system Processing/Alarms docs |
| CMD | Command construction, queuing, verification, uplink interfaces | CCSDS 232.0-B, 232.1-B, 231.0-B; reference mission-control system Commands/Queues/COP-1 docs |
| CFDP | File transfer service and lifecycle | CCSDS 727.0-B; reference mission-control system File Transfer docs |
| ARC | Telemetry/parameter/event/command-history archival and replay | reference mission-control system archive API docs |
| RTS | Real-time subscriptions to clients | reference mission-control system WebSocket and streaming docs |
| LIF | Instance and processor lifecycle control | reference mission-control system Instances and Processing docs |
| LNK | Data link control and monitoring | reference mission-control system Links docs |
| TIM | Time correlation and time-code handling | CCSDS 301.0-B; reference mission-control system Time Correlation docs |
| SEC | Authentication, authorization, TLS, audit | reference mission-control system IAM and Server Security docs |
| DIF | Differential conformance harness | Stakeholder brief §8 |
| QLT | Cross-cutting quality constraints (platform, performance, license) | Stakeholder brief §9 |

## 3. Out of scope

1. Ground station antenna/RF control.
2. Orbit determination, flight dynamics, conjunction analysis.
3. Mission planning and scheduling optimization.
4. Display rendering and UI composition (Cupola/Open MCT concern).
5. Flight software frameworks (cFS, F Prime, and similar).

## 4. Deferred (named, not specified in this baseline)

1. SDLS / link encryption profiles.
2. ECSS PUS coverage beyond a minimal mission-selected subset.
3. Replication and HA deployment topologies.

## 5. Pairing constraint

Tranquility shall not require Cupola-specific behavior. API compatibility is the only intended coupling point between products.

**Source:** User brief §4, §5, §10 (2026-07-15); External API documentation package (URL redacted)
