# Space Packet Processing and Decommutation — detailed design

The SPP subsystem receives raw telemetry from ground-station data links, extracts CCSDS space packets from frames, decodes the six-octet primary header for validity checking and APID-based routing, and drives model-driven decommutation through XTCE container resolution against the active mission database (MDB). Extracted parameter values (raw and engineering) flow into the parameter cache and out to subscribed clients over the WebSocket API. The design is deliberately deterministic: an immutable MDB instance plus pure extraction logic guarantees identical outputs for identical packet bytes and MDB version, which is verified by a differential replay harness in CI. All failure paths are explicit — structurally invalid packets produce machine-readable processing errors on the event stream and audit log, and packets with no matching MDB container are retained and flagged rather than silently dropped.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [packet-primary-header-decode.puml](packet-primary-header-decode.puml) | Nominal primary header decode, validity checks, and APID-based routing (incl. idle-packet discard) | L1-SPP-001, L2-SPP-001 |
| [xtce-container-resolution-nominal.puml](xtce-container-resolution-nominal.puml) | Nominal XTCE container resolution and parameter extraction for a packet mapped in the MDB | L1-SPP-001, L2-SPP-002 |
| [unmapped-packet-handling.puml](unmapped-packet-handling.puml) | Alternate flow: packet APID has no matching container in the active MDB — warning event, packet retained unprocessed | L1-SPP-001, L2-SPP-002 |
| [deterministic-replay-verification.puml](deterministic-replay-verification.puml) | Differential replay of a packet corpus with a pinned MDB version to verify deterministic output (pass/fail branches) | L1-SPP-001, L2-SPP-003 |
| [parse-failure-error-reporting.puml](parse-failure-error-reporting.puml) | Error flow: structurally invalid packet yields an explicit processing error with a machine-readable reason | L1-SPP-001, L2-SPP-004 |
