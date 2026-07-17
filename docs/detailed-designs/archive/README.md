# Archive — detailed design

The Archive (ARC) subsystem records realtime mission data — CCSDS space packets, decommutated parameter values, events, and command history — into time-partitioned archive stores, and serves it back through the documented HTTP API. Retrieval is offered in three forms: bounded parameter history queries (`GET /api/archive/{instance}/parameters/{name}`), chunked server-streaming replay (`POST /api/archive/{instance}:streamParameterValues`), and segment-level introspection of the parameter archive (`GET /api/parameter-archive/{instance}/pids/{pid}/segments`). When the parameter archive cannot satisfy a request — or the caller explicitly selects `source=replay` — a Replay Processor is created through the Processor Registry to re-read archived packets and re-decommutate them with the active MDB, so returned values always reflect current mission-database semantics. CQRS query handlers in the application layer mediate between the ASP.NET Core HTTP API and the infrastructure-layer archive stores; rejection paths (invalid time bounds, unknown instance/parameter/pid, client stream cancellation) are handled before or during archive reads so cursors and buffers are never leaked.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [archive-ingest-recording.puml](archive-ingest-recording.puml) | Recording of telemetry packets, parameter values, events, and command history into the archive stores | L1-ARC-001 |
| [parameter-history-retrieval.puml](parameter-history-retrieval.puml) | Nominal parameter history query with time bounds and documented query controls | L1-ARC-001, L2-ARC-001 |
| [parameter-history-invalid-request.puml](parameter-history-invalid-request.puml) | History query rejection paths: malformed time bounds, unknown instance, unknown parameter | L2-ARC-001 |
| [stream-parameter-values.puml](stream-parameter-values.puml) | Nominal chunked server-streaming parameter replay over a start/stop interval | L1-ARC-001, L2-ARC-002 |
| [stream-parameter-values-cancellation.puml](stream-parameter-values-cancellation.puml) | Stream request rejection and clean teardown on client disconnect mid-stream | L2-ARC-002 |
| [parameter-archive-segments.puml](parameter-archive-segments.puml) | Archived segment listing with start/end/count metadata, and unknown-pid rejection | L1-ARC-001, L2-ARC-003 |
| [replay-source-retrieval.puml](replay-source-retrieval.puml) | Explicit `source=replay` history retrieval via replay-mode processing with active MDB semantics | L1-ARC-001, L2-ARC-004 |
| [replay-source-coverage-gap.puml](replay-source-coverage-gap.puml) | Replay-mode fallback and merge when parameter archive coverage is incomplete | L2-ARC-004, L1-ARC-001 |
