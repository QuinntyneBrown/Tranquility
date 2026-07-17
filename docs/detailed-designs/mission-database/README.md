# Mission Database - detailed design

The Mission Database (MDB) subsystem ingests OMG XTCE 1.3 mission definitions and turns them into the authoritative runtime model that drives all telemetry and command interpretation in Tranquility. An XTCE Loader (infrastructure) parses the SpaceSystem tree, an XTCE Validator enforces structural and reference integrity before any activation, and a Support Matrix gate emits explicit unsupported-feature diagnostics with construct and location for anything outside the approved XTCE subset. Validated models are frozen into an immutable MDB Model, indexed by qualified name and alias namespace, and activated per instance through the Instance Registry (application layer). Operational clients query counts, space-system hierarchy, and parameter/command metadata over the documented HTTP MDB API resource model, and the same active model is consulted by the telemetry processor pipeline (container matching, decommutation, calibration) and the command encoder (argument validation, packet construction), ensuring mission portability without compiled mission logic.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [xtce-load-and-activate.puml](xtce-load-and-activate.puml) | Nominal XTCE load: parse, validate, build/index model, activate for the instance | L1-MDB-001, L2-MDB-001 |
| [xtce-load-rejected-broken-references.puml](xtce-load-rejected-broken-references.puml) | Load rejected: broken references produce a validation report; activation never occurs | L2-MDB-001, L1-MDB-001 |
| [xtce-load-unsupported-construct.puml](xtce-load-unsupported-construct.puml) | Load with constructs outside the approved support matrix: diagnostics identify construct and location | L2-MDB-004, L1-MDB-001 |
| [mdb-metadata-query.puml](mdb-metadata-query.puml) | MDB API metadata query: model counts and space-system hierarchy; 404 when no active database | L2-MDB-002, L1-MDB-001 |
| [mdb-parameter-resolution.puml](mdb-parameter-resolution.puml) | Parameter resolution by qualified name and by alias namespace (same entity resolved); unknown-name rejection | L2-MDB-003, L1-MDB-001 |
| [mdb-command-resolution.puml](mdb-command-resolution.puml) | Command resolution by qualified name and by alias namespace (same entity resolved); unknown-name rejection | L2-MDB-003, L1-MDB-001 |
| [mdb-runtime-application.puml](mdb-runtime-application.puml) | Active MDB applied at runtime: container-driven telemetry decommutation and XTCE-driven command encoding | L1-MDB-001 |
