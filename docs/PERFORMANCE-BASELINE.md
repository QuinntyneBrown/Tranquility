# Tranquility performance baseline

Declared numeric performance targets required by **L2-QLT-004** (parent
L1-QLT-002). These are the acceptance thresholds for performance work;
verification evidence is produced by `tests/Tranquility.Benchmarks`, whose
`thresholds.json` must carry these exact numbers (enforced by
`PerfBaselineDeclarationTests`).

Revision: 1 (2026-07-17, conservative defaults approved with the ATDD
implementation plan; closes TBD-013 / ADR-0005). Raising a target requires a
new revision of this document and matching thresholds.

| Metric | Key | Target | Unit |
|---|---|---|---|
| Sustained space-packet ingest rate | ingest-packets-per-second | 5000 | packets/s |
| Sustained parameter update rate | parameter-updates-per-second | 50000 | updates/s |
| End-to-end latency, UDP ingest to WebSocket delivery, p95 | e2e-latency-p95-ms | 250 | ms |
| End-to-end latency, UDP ingest to WebSocket delivery, p99 | e2e-latency-p99-ms | 500 | ms |
| Archive sustained write throughput | archive-write-mbps | 20 | MB/s |

Measurement conditions

- Release build on the CI Linux runner class; dedicated-run (`perf-full`)
  numbers are authoritative, PR `perf-smoke` gates at 50% of target to stay
  non-flaky.
- Ingest and latency metrics measured with the packet-generator corpus
  (`tests/fixtures/corpus`) against a realtime processor with an active
  sample mission database.
- Archive throughput measured as sustained payload bytes committed to the
  archive store over a 60 s window.
