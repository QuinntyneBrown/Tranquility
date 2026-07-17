# Tranquility detailed designs

Detailed-design PlantUML sequence diagrams per subsystem, mirroring the `docs/specs/` structure. Each subsystem folder contains one `.puml` diagram per behaviour/flow derived from that subsystem's L1/L2 requirements (nominal paths plus the error, rejection, and authorization-denial paths required by acceptance criteria), and a `README.md` mapping each diagram to the requirements it covers.

Every diagram carries a `' Requirements:` header comment; every L1 and L2 requirement ID in the specs baseline is covered by at least one diagram.

| Subsystem | Code | Diagrams | Index | Requirements |
|---|---|---|---|---|
| Application Programming Interface | API | 9 | [README](application-programming-interface/README.md) | [L1](../specs/application-programming-interface/L1.md) / [L2](../specs/application-programming-interface/L2.md) |
| Space Data Link Protocol | SDL | 5 | [README](space-data-link-protocol/README.md) | [L1](../specs/space-data-link-protocol/L1.md) / [L2](../specs/space-data-link-protocol/L2.md) |
| Space Packet Processing and Decommutation | SPP | 5 | [README](space-packet-processing-and-decommutation/README.md) | [L1](../specs/space-packet-processing-and-decommutation/L1.md) / [L2](../specs/space-packet-processing-and-decommutation/L2.md) |
| Mission Database | MDB | 7 | [README](mission-database/README.md) | [L1](../specs/mission-database/L1.md) / [L2](../specs/mission-database/L2.md) |
| Parameter Processing | PAR | 6 | [README](parameter-processing/README.md) | [L1](../specs/parameter-processing/L1.md) / [L2](../specs/parameter-processing/L2.md) |
| Commanding | CMD | 11 | [README](commanding/README.md) | [L1](../specs/commanding/L1.md) / [L2](../specs/commanding/L2.md) |
| CCSDS File Delivery Protocol | FDP | 9 | [README](ccsds-file-delivery-protocol/README.md) | [L1](../specs/ccsds-file-delivery-protocol/L1.md) / [L2](../specs/ccsds-file-delivery-protocol/L2.md) |
| Archive | ARC | 8 | [README](archive/README.md) | [L1](../specs/archive/L1.md) / [L2](../specs/archive/L2.md) |
| Real-time Streaming | RTS | 9 | [README](real-time-streaming/README.md) | [L1](../specs/real-time-streaming/L1.md) / [L2](../specs/real-time-streaming/L2.md) |
| Lifecycle | LIF | 9 | [README](lifecycle/README.md) | [L1](../specs/lifecycle/L1.md) / [L2](../specs/lifecycle/L2.md) |
| Data Links | LNK | 6 | [README](data-links/README.md) | [L1](../specs/data-links/L1.md) / [L2](../specs/data-links/L2.md) |
| Time Correlation | TIM | 7 | [README](time-correlation/README.md) | [L1](../specs/time-correlation/L1.md) / [L2](../specs/time-correlation/L2.md) |
| Security | SEC | 12 | [README](security/README.md) | [L1](../specs/security/L1.md) / [L2](../specs/security/L2.md) |
| Differential Conformance | DIF | 5 | [README](differential-conformance/README.md) | [L1](../specs/differential-conformance/L1.md) / [L2](../specs/differential-conformance/L2.md) |
| Quality and Governance | QLT | 8 | [README](quality-and-governance/README.md) | [L1](../specs/quality-and-governance/L1.md) / [L2](../specs/quality-and-governance/L2.md) |

Rendering: `java -jar plantuml.jar <subsystem>/*.puml` (or any PlantUML-compatible viewer).
