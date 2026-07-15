# ADR 0003: Archive storage engine

- **Status:** OPEN
- **Date:** 2026-07-15
- **Related:** TBD-015, L1-ARC-001

## Context

Archive behavior is externally constrained by API and replay semantics, but internal storage technology is not yet selected.

## Options

1. Relational store with time-partitioning.
2. Time-series optimized store.
3. Columnar/object hybrid architecture.

## Decision

OPEN — defer until workload and retention requirements are confirmed.

## Consequences to evaluate

- Sustained ingest throughput.
- Replay latency.
- Operational footprint and backup strategy.
