# ADR 0008: Clean-room protobuf authored from prose

- **Status:** ACCEPTED
- **Date:** 2026-07-17
- **Related:** TBD-012, L1-API-001, L2-API-003

## Context

The API must support the documented `json` and `protobuf` WebSocket
subprotocols (L2-API-003). TBD-012 flagged the handling of the reference
system's `.proto` interface definitions as a blocking legal question.

## Decision

Author Tranquility's own `.proto` message set in `src/Tranquility.Wire/Protos/`
derived **solely** from published prose API documentation and this repo's ICD
(ICD §6 option 1). No reference-system `.proto` artifacts are consulted or
vendored. Both `json` and `protobuf` subprotocols are implemented over this
schema; the JSON and protobuf encodings share one documented camelCase shape.

## Consequences

- L2-API-003 is fully satisfied without depending on the unresolved legal
  question; TBD-012's escalation posture is preserved (we never touch the
  reference definitions).
- Wire ambiguity where prose is silent is recorded via the differential
  harness triage class "standard ambiguity" (L2-DIF-003), not guessed.
- The single `WireMapper`/`ProtoMapper` boundary is round-trip tested so the
  two encodings never drift.
