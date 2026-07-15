# ADR 0002: WebSocket transport choice

- **Status:** PROPOSED
- **Date:** 2026-07-15 (updated by vertical-slice implementation)
- **Related:** TBD-014, L1-API-001, L1-RTS-001

## Context

The baseline requires reference mission-control system-compatible WebSocket behavior (`/api/websocket`, call/seq semantics, JSON/protobuf subprotocols). Internal transport stack choice remains open.

## Options

1. Native ASP.NET WebSocket handling only.
2. SignalR gateway with explicit compatibility adapter.
3. Hybrid model (native compatibility endpoint + internal SignalR fan-out).

## Decision

PROPOSED — Option 1 (native `System.Net.WebSockets`).

Rationale from the vertical-slice implementation:

- The ICD requires a raw WebSocket endpoint at `/api/websocket` with `json`
  subprotocol negotiation and message-level call/seq correlation. SignalR
  imposes its own handshake, framing, and hub protocol on the wire, which is
  not compatible with that contract; an adapter would re-implement raw
  WebSocket handling anyway.
- The slice implements the contract directly in
  `src/Tranquility.Server/WebSockets/WebSocketApiHandler.cs` with no
  additional dependency, keeping the license-audit surface smaller (L2-QLT-003).

Remains PROPOSED (not ACCEPTED) until the protobuf subprotocol decision
(TBD-012) is settled, since that outcome could reshape transport needs.

## Consequences to evaluate

- Wire compatibility risk — reduced: no framing layer between server and contract.
- Operational complexity — connection management is hand-rolled; revisit if
  scale-out fan-out is needed (SignalR backplane would not be usable on the
  compatibility endpoint regardless).
- Performance and observability — no benchmarks asserted (TBD-013).
