# TRQ-VCRM

## 1. Generation basis

This matrix is generated from requirement metadata in subsystem `L1.md` and `L2.md` artifacts under `docs/specs/`.

- Baseline date: 2026-07-15
- Total requirements: 87
- L1 requirements: 20
- L2 requirements: 67

## 2. Verification cross-reference matrix

| ID | Parent | Children | Verification | Artifact |
|---|---|---|---|---|
| L1-API-001 | TRQ-HLR | L2-API-001, L2-API-002, L2-API-003 | Demonstration | docs/specs/api/L1.md |
| L1-API-002 | TRQ-HLR | L2-API-004, L2-API-005 | Test | docs/specs/api/L1.md |
| L1-SDL-001 | TRQ-HLR | L2-SDL-001, L2-SDL-002, L2-SDL-003, L2-SDL-004 | Test | docs/specs/sdlp/L1.md |
| L1-SDL-002 | TRQ-HLR | L2-SDL-005 | Demonstration | docs/specs/sdlp/L1.md |
| L1-SPP-001 | TRQ-HLR | L2-SPP-001, L2-SPP-002, L2-SPP-003, L2-SPP-004 | Test | docs/specs/decomm/L1.md |
| L1-MDB-001 | TRQ-HLR | L2-MDB-001, L2-MDB-002, L2-MDB-003, L2-MDB-004 | Test | docs/specs/mdb/L1.md |
| L1-PAR-001 | TRQ-HLR | L2-PAR-001, L2-PAR-002, L2-PAR-003, L2-PAR-004 | Test | docs/specs/parameters/L1.md |
| L1-CMD-001 | TRQ-HLR | L2-CMD-001, L2-CMD-002, L2-CMD-003, L2-CMD-004 | Demonstration | docs/specs/commanding/L1.md |
| L1-CMD-002 | TRQ-HLR | L2-CMD-005, L2-CMD-006 | Test | docs/specs/commanding/L1.md |
| L1-FDP-001 | TRQ-HLR | L2-FDP-001, L2-FDP-002, L2-FDP-003, L2-FDP-004 | Demonstration | docs/specs/cfdp/L1.md |
| L1-ARC-001 | TRQ-HLR | L2-ARC-001, L2-ARC-002, L2-ARC-003, L2-ARC-004 | Demonstration | docs/specs/archive/L1.md |
| L1-RTS-001 | TRQ-HLR | L2-RTS-001, L2-RTS-002, L2-RTS-003, L2-RTS-004 | Demonstration | docs/specs/streaming/L1.md |
| L1-LIF-001 | TRQ-HLR | L2-LIF-001, L2-LIF-002, L2-LIF-003, L2-LIF-004 | Demonstration | docs/specs/lifecycle/L1.md |
| L1-LNK-001 | TRQ-HLR | L2-LNK-001, L2-LNK-002, L2-LNK-003, L2-LNK-004 | Demonstration | docs/specs/links/L1.md |
| L1-TIM-001 | TRQ-HLR | L2-TIM-001, L2-TIM-002, L2-TIM-003, L2-TIM-004 | Demonstration | docs/specs/time/L1.md |
| L1-SEC-001 | TRQ-HLR | L2-SEC-001, L2-SEC-002, L2-SEC-003 | Test | docs/specs/security/L1.md |
| L1-SEC-002 | TRQ-HLR | L2-SEC-004, L2-SEC-005 | Demonstration | docs/specs/security/L1.md |
| L1-DIF-001 | TRQ-HLR | L2-DIF-001, L2-DIF-002, L2-DIF-003, L2-DIF-004 | Demonstration | docs/specs/differential/L1.md |
| L1-QLT-001 | TRQ-HLR | L2-QLT-001, L2-QLT-002, L2-QLT-003, L2-QLT-006 | Inspection | docs/specs/quality/L1.md |
| L1-QLT-002 | TRQ-HLR | L2-QLT-004, L2-QLT-005 | Test | docs/specs/quality/L1.md |
| L2-API-001 | L1-API-001 | - | Test | docs/specs/api/L2.md |
| L2-API-002 | L1-API-001 | - | Test | docs/specs/api/L2.md |
| L2-API-003 | L1-API-001 | - | Test | docs/specs/api/L2.md |
| L2-API-004 | L1-API-002 | - | Test | docs/specs/api/L2.md |
| L2-API-005 | L1-API-002 | - | Inspection | docs/specs/api/L2.md |
| L2-SDL-001 | L1-SDL-001 | - | Test | docs/specs/sdlp/L2.md |
| L2-SDL-002 | L1-SDL-001 | - | Test | docs/specs/sdlp/L2.md |
| L2-SDL-003 | L1-SDL-001 | - | Test | docs/specs/sdlp/L2.md |
| L2-SDL-004 | L1-SDL-001 | - | Test | docs/specs/sdlp/L2.md |
| L2-SDL-005 | L1-SDL-002 | - | Demonstration | docs/specs/sdlp/L2.md |
| L2-SPP-001 | L1-SPP-001 | - | Test | docs/specs/decomm/L2.md |
| L2-SPP-002 | L1-SPP-001 | - | Test | docs/specs/decomm/L2.md |
| L2-SPP-003 | L1-SPP-001 | - | Analysis | docs/specs/decomm/L2.md |
| L2-SPP-004 | L1-SPP-001 | - | Demonstration | docs/specs/decomm/L2.md |
| L2-MDB-001 | L1-MDB-001 | - | Test | docs/specs/mdb/L2.md |
| L2-MDB-002 | L1-MDB-001 | - | Test | docs/specs/mdb/L2.md |
| L2-MDB-003 | L1-MDB-001 | - | Test | docs/specs/mdb/L2.md |
| L2-MDB-004 | L1-MDB-001 | - | Inspection | docs/specs/mdb/L2.md |
| L2-PAR-001 | L1-PAR-001 | - | Test | docs/specs/parameters/L2.md |
| L2-PAR-002 | L1-PAR-001 | - | Test | docs/specs/parameters/L2.md |
| L2-PAR-003 | L1-PAR-001 | - | Test | docs/specs/parameters/L2.md |
| L2-PAR-004 | L1-PAR-001 | - | Demonstration | docs/specs/parameters/L2.md |
| L2-CMD-001 | L1-CMD-001 | - | Test | docs/specs/commanding/L2.md |
| L2-CMD-002 | L1-CMD-001 | - | Demonstration | docs/specs/commanding/L2.md |
| L2-CMD-003 | L1-CMD-001 | - | Test | docs/specs/commanding/L2.md |
| L2-CMD-004 | L1-CMD-001 | - | Test | docs/specs/commanding/L2.md |
| L2-CMD-005 | L1-CMD-002 | - | Inspection | docs/specs/commanding/L2.md |
| L2-CMD-006 | L1-CMD-002 | - | Test | docs/specs/commanding/L2.md |
| L2-FDP-001 | L1-FDP-001 | - | Test | docs/specs/cfdp/L2.md |
| L2-FDP-002 | L1-FDP-001 | - | Demonstration | docs/specs/cfdp/L2.md |
| L2-FDP-003 | L1-FDP-001 | - | Test | docs/specs/cfdp/L2.md |
| L2-FDP-004 | L1-FDP-001 | - | Inspection | docs/specs/cfdp/L2.md |
| L2-ARC-001 | L1-ARC-001 | - | Test | docs/specs/archive/L2.md |
| L2-ARC-002 | L1-ARC-001 | - | Demonstration | docs/specs/archive/L2.md |
| L2-ARC-003 | L1-ARC-001 | - | Test | docs/specs/archive/L2.md |
| L2-ARC-004 | L1-ARC-001 | - | Test | docs/specs/archive/L2.md |
| L2-RTS-001 | L1-RTS-001 | - | Test | docs/specs/streaming/L2.md |
| L2-RTS-002 | L1-RTS-001 | - | Test | docs/specs/streaming/L2.md |
| L2-RTS-003 | L1-RTS-001 | - | Demonstration | docs/specs/streaming/L2.md |
| L2-RTS-004 | L1-RTS-001 | - | Analysis | docs/specs/streaming/L2.md |
| L2-LIF-001 | L1-LIF-001 | - | Test | docs/specs/lifecycle/L2.md |
| L2-LIF-002 | L1-LIF-001 | - | Demonstration | docs/specs/lifecycle/L2.md |
| L2-LIF-003 | L1-LIF-001 | - | Test | docs/specs/lifecycle/L2.md |
| L2-LIF-004 | L1-LIF-001 | - | Inspection | docs/specs/lifecycle/L2.md |
| L2-LNK-001 | L1-LNK-001 | - | Test | docs/specs/links/L2.md |
| L2-LNK-002 | L1-LNK-001 | - | Demonstration | docs/specs/links/L2.md |
| L2-LNK-003 | L1-LNK-001 | - | Test | docs/specs/links/L2.md |
| L2-LNK-004 | L1-LNK-001 | - | Inspection | docs/specs/links/L2.md |
| L2-TIM-001 | L1-TIM-001 | - | Test | docs/specs/time/L2.md |
| L2-TIM-002 | L1-TIM-001 | - | Demonstration | docs/specs/time/L2.md |
| L2-TIM-003 | L1-TIM-001 | - | Test | docs/specs/time/L2.md |
| L2-TIM-004 | L1-TIM-001 | - | Analysis | docs/specs/time/L2.md |
| L2-SEC-001 | L1-SEC-001 | - | Test | docs/specs/security/L2.md |
| L2-SEC-002 | L1-SEC-001 | - | Test | docs/specs/security/L2.md |
| L2-SEC-003 | L1-SEC-001 | - | Test | docs/specs/security/L2.md |
| L2-SEC-004 | L1-SEC-002 | - | Inspection | docs/specs/security/L2.md |
| L2-SEC-005 | L1-SEC-002 | - | Demonstration | docs/specs/security/L2.md |
| L2-DIF-001 | L1-DIF-001 | - | Test | docs/specs/differential/L2.md |
| L2-DIF-002 | L1-DIF-001 | - | Analysis | docs/specs/differential/L2.md |
| L2-DIF-003 | L1-DIF-001 | - | Inspection | docs/specs/differential/L2.md |
| L2-DIF-004 | L1-DIF-001 | - | Inspection | docs/specs/differential/L2.md |
| L2-QLT-001 | L1-QLT-001 | - | Inspection | docs/specs/quality/L2.md |
| L2-QLT-002 | L1-QLT-001 | - | Analysis | docs/specs/quality/L2.md |
| L2-QLT-003 | L1-QLT-001 | - | Test | docs/specs/quality/L2.md |
| L2-QLT-004 | L1-QLT-002 | - | Inspection | docs/specs/quality/L2.md |
| L2-QLT-005 | L1-QLT-002 | - | Inspection | docs/specs/quality/L2.md |
| L2-QLT-006 | L1-QLT-001 | - | Analysis | docs/specs/quality/L2.md |

## 3. Self-audit

| Check | Count | Result |
|---|---:|---|
| Total requirements | 87 | Complete |
| L2 requirements without parent | 0 | Pass |
| L1 requirements without child | 0 | Pass |
| Requirements missing Source field | 0 | Pass |
| Requirements with unresolved `TBD-nnn` source placeholders | 23 | Open (tracked in TRQ-OPEN-QUESTIONS.md) |

## 4. Notes

1. `TBD-nnn` placeholders are intentional unresolved items with owners and blocking flags in `TRQ-OPEN-QUESTIONS.md`.
2. This VCRM traces requirement lineage only; it does not declare requirement closure.
