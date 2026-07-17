# Lifecycle — detailed design

The Lifecycle (LIF) subsystem provides runtime control of Tranquility mission execution contexts: instances (per-mission processing environments carrying a mission database, data links, and processors) and processors (real-time or archive-replay processing pipelines within an instance). All operations are exposed through the documented HTTP API, with state changes additionally pushed to attached clients over the WebSocket API. Requests enter the ASP.NET Core host, are dispatched to CQRS command/query handlers in the application layer, and act on the Instance Registry and Processor Registry; replay processors are constructed by a Processor Factory, drive a Replay Engine over the Archive Store, and publish state transitions through the Subscription Manager. Lifecycle-mutating operations are recorded in the Audit Log. The diagrams below cover every nominal flow plus the rejection, denial, and alternate paths implied by the L2 acceptance criteria (unknown instance/processor, duplicate or invalid create requests, non-replay edit rejection, protected-processor deletion denial, and persistent vs non-persistent disposal semantics).

| Diagram | Behaviour | Requirements |
|---|---|---|
| [instance-list.puml](instance-list.puml) | List configured instances with state and metadata in documented structure | L1-LIF-001, L2-LIF-001 |
| [instance-detail.puml](instance-detail.puml) | Retrieve single instance detail; 404 rejection for unknown instance | L1-LIF-001, L2-LIF-001 |
| [instance-lifecycle-control.puml](instance-lifecycle-control.puml) | Start / stop / restart an instance; conflict rejection on invalid transition | L1-LIF-001 |
| [processor-create.puml](processor-create.puml) | Create a processor; rejections for unknown instance, duplicate name, unknown type | L1-LIF-001, L2-LIF-002 |
| [processor-list.puml](processor-list.puml) | List processors of an instance; 404 rejection for unknown instance | L1-LIF-001, L2-LIF-002 |
| [processor-edit.puml](processor-edit.puml) | Edit processor state / seek / speed; rejection for non-replay processor | L1-LIF-001, L2-LIF-002 |
| [processor-delete.puml](processor-delete.puml) | Delete a processor; denial for protected processors | L1-LIF-001, L2-LIF-002 |
| [processor-persistent-semantics.puml](processor-persistent-semantics.puml) | Persistent processor survives last-client detach; non-persistent processor auto-disposed | L1-LIF-001, L2-LIF-003 |
| [replay-state-transitions.puml](replay-state-transitions.puml) | Replay start / pause / resume / stop reflected in exposed processor state fields | L1-LIF-001, L2-LIF-004 |
