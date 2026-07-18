# ADR 0004: Time correlation model

- **Status:** ACCEPTED
- **Date:** 2026-07-17 (supersedes the OPEN baseline record)
- **Related:** TBD-009, TBD-016, L1-TIM-001

## Decision

A linear onboard-to-UTC model `utc = offset + gradient * obt`, fit by ordinary
least squares over a bounded sample ring (default 5). Deviation policy: within
accuracy promotes silently, within validity promotes with a warning, beyond
validity retains the prior coefficients. Declared time-code profile
(L2-TIM-004): OBT as CUC 4+2 octets against the agency epoch, ground
timestamps as CDS 8-octet.

## Consequences

Deterministic timestamp conversion (the fit is pure); operators can override
coefficients manually; TOF intervals apply a covering one-way delay with a
default fallback.
