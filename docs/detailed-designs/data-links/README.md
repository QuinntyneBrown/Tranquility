# Data Links — detailed design

The Data Links (LNK) subsystem exposes control and status telemetry for mission data links through the documented HTTP API. Requests enter via the ASP.NET Core HTTP API layer, are dispatched as CQRS queries/commands to application-layer handlers (ListLinksQueryHandler, Enable/Disable/ResetCounters/RunAction command handlers), which resolve the target instance through the Instance Registry and reach concrete link implementations (UDP TM/TC links, adapter-backed links) via the Link Manager. Control-plane operations (enable, disable, resetCounters, run-action) are privilege-gated by the Authorization Service and recorded in the Audit Log; status queries surface the link information model — name, disabled state, monotonic in/out counters, and detail fields — which the infrastructure links maintain as traffic flows to and from the ground station. Error paths (unknown instance, unknown link, unadvertised action, adapter failure) and authorization denials are modeled as alt branches in the corresponding diagrams.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [link-list-status.puml](link-list-status.puml) | List links with metadata and counters via `GET /api/links/{instance}`; unknown-instance rejection | L1-LNK-001, L2-LNK-001, L2-LNK-004 |
| [link-enable.puml](link-enable.puml) | Enable a link (idempotent); unknown-link and privilege-denial paths | L1-LNK-001, L2-LNK-002 |
| [link-disable.puml](link-disable.puml) | Disable a link and reflect the new state in subsequent status queries; unknown-link and privilege-denial paths | L1-LNK-001, L2-LNK-002 |
| [link-reset-counters.puml](link-reset-counters.puml) | Reset link in/out counters to zero; unknown-link and privilege-denial paths | L1-LNK-001, L2-LNK-002, L2-LNK-004 |
| [link-run-action.puml](link-run-action.puml) | Execute an advertised link custom action per method contract; unadvertised-action rejection, adapter failure, privilege denial | L1-LNK-001, L2-LNK-003 |
| [link-counter-update.puml](link-counter-update.puml) | Inbound/outbound counter accumulation on live traffic and publication through the link information model | L1-LNK-001, L2-LNK-004 |
