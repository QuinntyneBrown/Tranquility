# TRQ-OPEN-QUESTIONS

## 1. Purpose

This register tracks unresolved requirements gaps, legal escalations, and all `TBD-nnn` placeholders used in the baseline.

## 2. Open questions register

| ID | Topic | Question | Proposed default (non-binding) | Owner | Blocking |
|---|---|---|---|---|---|
| TBD-001 | CCSDS 133.0-B clause mapping | Which exact CCSDS 133.0-B clauses map to packet-header parsing requirements in `decomm/L2.md`? | Use current requirement text; verify clause numbers before implementation freeze. | Systems Engineering | Yes |
| TBD-002 | CCSDS 132.0-B profile | Which TM transfer frame options (OCF/FECF/virtual channel handling) are selected for baseline profile? | Start with a single mission profile; record alternatives in ADR 0001/0006. | Systems Engineering | Yes |
| TBD-003 | CCSDS 732.0-B profile | Which AOS options (insert zone, virtual channel usage) are selected? | Single baseline AOS profile with explicit configuration object. | Systems Engineering | Yes |
| TBD-004 | CCSDS 732.1-B profile | Is USLP required in baseline v1 or profile-gated as optional? | Include profile placeholder; gate by mission configuration. | Systems Engineering | No |
| TBD-005 | CCSDS 232.0-B profile | Which TC frame profile and mode are required for baseline missions? | Select one profile per mission ICD; keep interface stable. | Systems Engineering | Yes |
| TBD-006 | CCSDS 232.1-B COP-1 profile | Which COP-1 operational mode and parameters are baseline-required? | Require one default COP-1 profile and mission overrides. | Commanding Lead | Yes |
| TBD-007 | CCSDS 231.0-B CLTU profile | Which CLTU coding/synchronization options are baseline-required? | Require one CLTU profile per mission link configuration. | Commanding Lead | Yes |
| TBD-008 | CCSDS 727.0-B CFDP profile | Which CFDP class/features are in baseline (class 1/2, acknowledged options, segmentation details)? | Baseline minimal interoperable profile; advanced features deferred. | File Transfer Lead | Yes |
| TBD-009 | CCSDS 301.0-B clause mapping | Which exact CCSDS time-code clauses are mandatory for baseline decode/encode behavior? | Define one time-code profile and verify with mission test vectors. | Timing Lead | Yes |
| TBD-010 | XTCE clause mapping | Which XTCE constructs are mandatory in baseline support matrix? | Support required mission subset; reject unsupported constructs explicitly. | MDB Lead | Yes |
| TBD-011 | ECSS PUS minimal set | Which ECSS-E-ST-70-41C services/capability sets are included in minimal scope? | Include only mission-required minimal subset; document by service ID. | Systems Engineering | Yes |
| TBD-012 | Legal: API definition source | May Tranquility consume or vendor reference mission-control system `.proto` definitions, or must interface be derived from prose docs only? | No implementation commitment until counsel decision. | Product + Counsel | **Yes (Hard Blocker)** |
| TBD-013 | Performance targets | What sustained ingest, parameter update, p95/p99 latency, and archive throughput targets are approved? | Use placeholder SLOs in `quality/L2.md` pending approval. | Product + Performance Lead | Yes |
| TBD-014 | WebSocket transport stack | Is native WebSocket sufficient, or is SignalR required while preserving wire compatibility? | Prefer native WebSocket unless compatibility analysis fails. | Architecture Board | No |
| TBD-015 | Archive storage engine | Which archive persistence technology is selected for baseline? | Defer engine choice; preserve external API contract. | Architecture Board | No |
| TBD-016 | Time correlation model | Which coefficient update policy and deviation thresholds are mission baseline? | Use configurable thresholds with documented defaults. | Timing Lead | No |
| TBD-017 | CQRS boundary | What command/query boundary granularity is required by architecture governance? | Keep boundary at public service layer until further decomposition. | Architecture Board | No |
| TBD-018 | API method baseline set | Which exact reference mission-control system-documented methods are required for v1 conformance declaration? | Cover all methods listed in in-scope resource families; phase remainder by revision. | Product + API Lead | Yes |
| TBD-019 | Differential dataset corpus | Which packet/command/reference datasets are canonical for differential conformance? | Start with one nominal and one fault-injection corpus per mission profile. | QA Lead | Yes |
| TBD-020 | License-audit tooling | Which CI license scanner(s) and policy gates are mandated? | Enforce at least one SPDX-capable scanner with fail-on-policy-violation. | DevSecOps | No |

## 3. Legal escalation note (non-resolved)

`TBD-012` is intentionally unresolved here. This document records options and ownership only. It does not provide legal advice or legal conclusions.

## 4. Counting rule

Any requirement source containing `TBD-nnn` is considered unresolved until this register entry is closed by accountable owner and date.
