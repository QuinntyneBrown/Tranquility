# ADR 0002: WebSocket transport choice

- **Status:** OPEN
- **Date:** 2026-07-15
- **Related:** TBD-014, L1-API-001, L1-RTS-001

## Context

The baseline requires reference mission-control system-compatible WebSocket behavior (`/api/websocket`, call/seq semantics, JSON/protobuf subprotocols). Internal transport stack choice remains open.

## Options

1. Native ASP.NET WebSocket handling only.
2. SignalR gateway with explicit compatibility adapter.
3. Hybrid model (native compatibility endpoint + internal SignalR fan-out).

## Decision

OPEN — compatibility and complexity trade study required.

## Consequences to evaluate

- Wire compatibility risk.
- Operational complexity.
- Performance and observability.
