# ADR 0005: Performance target baseline

- **Status:** ACCEPTED
- **Date:** 2026-07-17 (supersedes the OPEN baseline record)
- **Related:** TBD-013, L1-QLT-002, L2-QLT-004

## Decision

Declared conservative targets (see `docs/PERFORMANCE-BASELINE.md`): sustained
ingest ≥ 5,000 packets/s; ≥ 50,000 parameter updates/s; UDP→WebSocket latency
p95 ≤ 250 ms / p99 ≤ 500 ms; archive write ≥ 20 MB/s.

## Verification

`tests/Tranquility.Benchmarks/thresholds.json` mirrors these numbers (equality
enforced by `PerfBaselineDeclarationTests`). The deterministic Core throughput
is gated on every PR (`PerfVerificationTests`, Category=PerfSmoke) and by the
standalone benchmark in the nightly `perf-full` job. Measured Core throughput
sits well above target (hundreds of thousands of packets/s), leaving headroom;
targets are revisable upward by a new revision of the baseline document.
