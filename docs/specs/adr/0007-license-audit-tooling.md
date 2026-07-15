# ADR 0007: License audit tooling in CI

- **Status:** OPEN
- **Date:** 2026-07-15
- **Related:** TBD-020, L2-QLT-003

## Context

The baseline requires CI enforcement that dependency licenses remain compatible with Apache-2.0 distribution intent.

## Options

1. Single scanner with strict fail-on-violation policy.
2. Dual-scanner approach for cross-validation.
3. SBOM-first workflow with policy engine.

## Decision

OPEN — tooling and policy threshold pending DevSecOps decision.

## Consequences to evaluate

- False positive/negative rates.
- CI execution time.
- Audit traceability for releases.
