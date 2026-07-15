# TRQ-HLR

## 1. Purpose

This document is the high-level requirement index for Tranquility. Detailed requirements are allocated by subsystem in `docs/specs/<subsystem>/L2.md`.

## 2. High-level requirements index

| ID | Requirement (shall statement) | Source | Allocated specification |
|---|---|---|---|
| L1-API-001 | Tranquility shall expose an HTTP and WebSocket interface wire-compatible with documented reference mission-control system API behavior. | External API documentation package (URL redacted) | `api/L1.md` |
| L1-API-002 | Tranquility shall preserve documented API wire semantics for serialization, timestamps, and error envelopes. | External API documentation package (URL redacted) | `api/L1.md` |
| L1-SDL-001 | Tranquility shall process TM, AOS, USLP, and TC transfer frames using mission-selected CCSDS profiles. | CCSDS 132.0-B, 732.0-B, 732.1-B, 232.0-B (TBD-002..TBD-005) | `sdlp/L1.md` |
| L1-SDL-002 | Tranquility shall report invalid frame conditions through explicit diagnostics. | CCSDS data-link standards (TBD-002..TBD-005) | `sdlp/L1.md` |
| L1-SPP-001 | Tranquility shall parse and decommutate CCSDS space packets into mission-defined data objects. | CCSDS 133.0-B (TBD-001); OMG XTCE 1.3 (TBD-010) | `decomm/L1.md` |
| L1-MDB-001 | Tranquility shall load and apply XTCE mission database definitions for telemetry and command processing. | OMG XTCE 1.3 | `mdb/L1.md` |
| L1-PAR-001 | Tranquility shall evaluate parameter values, limits, alarms, and derived results from mission definitions. | OMG XTCE 1.3; reference mission-control system processing/alarms API docs | `parameters/L1.md` |
| L1-CMD-001 | Tranquility shall support command issue, queueing, dispatch, and verification workflows. | CCSDS 232.0-B, 232.1-B, 231.0-B; reference mission-control system commands/queues docs | `commanding/L1.md` |
| L1-CMD-002 | Tranquility shall enforce privileged control for command-path override operations. | reference mission-control system Issue Command docs; stakeholder brief §9 | `commanding/L1.md` |
| L1-FDP-001 | Tranquility shall provide CFDP-compatible file transfer operations through the public API. | CCSDS 727.0-B (TBD-008); reference mission-control system file-transfer docs | `cfdp/L1.md` |
| L1-ARC-001 | Tranquility shall archive and replay telemetry, parameters, events, and command history. | reference mission-control system parameter archive and history docs | `archive/L1.md` |
| L1-RTS-001 | Tranquility shall provide real-time subscription streaming for operational client use. | reference mission-control system WebSocket and streaming docs | `streaming/L1.md` |
| L1-LIF-001 | Tranquility shall support lifecycle management of instances and processors via API. | reference mission-control system instances and processing docs | `lifecycle/L1.md` |
| L1-LNK-001 | Tranquility shall provide API control and status telemetry for mission data links. | reference mission-control system links docs | `links/L1.md` |
| L1-TIM-001 | Tranquility shall provide onboard-to-UTC time correlation interfaces and CCSDS time handling. | reference mission-control system time-correlation docs; CCSDS 301.0-B (TBD-009) | `time/L1.md` |
| L1-SEC-001 | Tranquility shall enforce authentication and authorization for API access. | reference mission-control system IAM docs; server security manual | `security/L1.md` |
| L1-SEC-002 | Tranquility shall provide TLS transport protection and auditable security records. | reference mission-control system overview/security docs; stakeholder brief §9 | `security/L1.md` |
| L1-DIF-001 | Tranquility shall include a differential conformance harness comparing external behavior to a stock reference mission-control system deployment. | Stakeholder brief §8 | `differential/L1.md` |
| L1-QLT-001 | Tranquility shall satisfy baseline platform, architecture, and license-governance constraints. | Stakeholder brief §9 | `quality/L1.md` |
| L1-QLT-002 | Tranquility shall satisfy confirmed performance targets and explicitly track unresolved target values. | Stakeholder brief §9; TBD-013 | `quality/L1.md` |

## 3. Review gate status

`TRQ-HLR` is complete for baseline expansion. Detailed decomposition and acceptance criteria are provided in subsystem `L2.md` files.
