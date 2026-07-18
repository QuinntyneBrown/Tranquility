# ADR 0002: WebSocket transport choice

- **Status:** ACCEPTED
- **Date:** 2026-07-17 (supersedes the PROPOSED baseline record)
- **Related:** TBD-014, L1-API-001, L1-RTS-001

## Decision

Native `System.Net.WebSockets` at `/api/websocket`, with hand-rolled
subprotocol negotiation (json/protobuf), per-session call/seq correlation, a
bounded outbound buffer, and cancel/state built-ins
(`src/Tranquility.Server/WebSockets/`). No SignalR.

## Rationale

The ICD requires a raw endpoint with documented framing and message-level
call/seq semantics; SignalR imposes its own handshake and hub protocol, which
is incompatible with that contract. The native handler adds no dependency,
keeping the license-audit surface small (L2-QLT-003), and the drop-detectability
requirement (L2-RTS-004) is met directly through the bounded buffer.
