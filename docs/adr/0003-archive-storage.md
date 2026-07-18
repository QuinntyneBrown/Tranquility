# ADR 0003: Archive storage engine

- **Status:** ACCEPTED
- **Date:** 2026-07-17 (supersedes the OPEN baseline record)
- **Related:** TBD-015, L1-ARC-001

## Decision

One SQLite database per instance (`data/<instance>/archive.db`) in WAL mode,
with a single writer task per store, a parameter-value segment index, and a
raw `tm_packet` table backing replay-source retrieval. IAM and the audit hash
chain live in a separate `system.db`.

## Rationale

Zero operational footprint, Linux-first, identical behaviour in CI and
production, and comfortably above the declared archive-write target
(ADR-0005). The external archive API contract (L2-ARC-001..004) is independent
of this choice, so the engine can be revisited without wire impact.

## Consequences

- The transitive native SQLite bundle is pinned to a patched version; the CI
  license/vulnerability audit gates on it.
- Sustained-throughput escape hatches (per-instance DB files already in place;
  segment-blob packing) remain available if a mission exceeds the baseline.
