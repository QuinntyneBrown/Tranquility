# ADR 0004: Time correlation profile

- **Status:** OPEN
- **Date:** 2026-07-15
- **Related:** TBD-009, TBD-016, L1-TIM-001

## Context

The API requires time-correlation status/configuration semantics, but mission-specific model details and thresholds are not yet fixed.

## Options

1. Fixed linear model with configurable coefficients.
2. Piecewise model with mission-defined update windows.
3. Pluggable correlation model contract.

## Decision

OPEN — select after mission timing analysis.

## Consequences to evaluate

- Determinism of timestamp conversion.
- Operational tuning burden.
- Verification complexity.
