# Real-time Streaming — detailed design

The Real-time Streaming (RTS) subsystem delivers low-latency push updates to browser clients over the documented WebSocket API. A single WebSocket session multiplexes many topic subscriptions: each client request carries an `id`, the server answers with a `reply` that binds it to a server-assigned `call` number, and every subsequent stream message carries that `call` plus a session-monotonic `seq` so clients can deterministically correlate interleaved traffic and detect dropped messages. The design routes subscribe/cancel calls from the WebSocket API through a Session Manager and Topic Registry into the application-layer Subscription Manager, which registers listeners against the Telemetry Processor (parameter deliveries via the Decommutation Engine and Parameter Cache), the Instance Registry (processor state), and the Link Manager (link state); an outbound message buffer per session assigns `seq` before enqueue so overload-induced drops leave an observable sequence discontinuity rather than silent loss.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [websocket-session-establish.puml](websocket-session-establish.puml) | WebSocket upgrade, session creation, topic subscribe with reply correlation; unknown-topic rejection | L1-RTS-001 |
| [call-correlation-multiplexed-subscriptions.puml](call-correlation-multiplexed-subscriptions.puml) | Concurrent subscriptions on one socket; every server message carries call/seq for deterministic correlation | L2-RTS-001, L1-RTS-001 |
| [parameters-subscription-nominal.puml](parameters-subscription-nominal.puml) | Parameters topic: subscribe, telemetry ingest, decommutation, filtered parameter updates emitted on the stream | L2-RTS-002, L1-RTS-001 |
| [parameters-subscription-invalid-request.puml](parameters-subscription-invalid-request.puml) | Parameters topic: invalid instance/processor or unresolvable parameter rejected with correlated exception reply | L2-RTS-002, L1-RTS-001 |
| [processor-state-subscription.puml](processor-state-subscription.puml) | Processors topic: runtime processor state transitions pushed without polling | L2-RTS-003, L1-RTS-001 |
| [link-state-subscription.puml](link-state-subscription.puml) | Links topic: link status and counter changes pushed without polling | L2-RTS-003, L1-RTS-001 |
| [sequence-drop-detection.puml](sequence-drop-detection.puml) | Induced drop under load: seq assigned before enqueue, overflow drop leaves observable seq discontinuity | L2-RTS-004, L1-RTS-001 |
| [subscription-cancel.puml](subscription-cancel.puml) | Cancel a single call by call number; unknown-call rejection; other calls unaffected | L1-RTS-001, L2-RTS-001 |
| [websocket-disconnect-cleanup.puml](websocket-disconnect-cleanup.puml) | Graceful or abrupt disconnect triggers cancellation of all session subscriptions and session teardown | L1-RTS-001 |
