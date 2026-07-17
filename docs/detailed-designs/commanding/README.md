# Commanding - detailed design

The Commanding subsystem carries a command from operator intent to spacecraft execution and back. An operator issues a command through the documented HTTP endpoint (`POST /api/processors/{instance}/{processor}/commands/{name}`); the application layer validates arguments against the MDB command definition, encodes the CCSDS TC binary, and places the command on a named command queue. Queue methods expose inspection plus accept/reject release control; accepted entries flow to the dispatcher, which drives the core-domain COP-1 FOP engine (CCSDS 232.1-B mission profile) and CLTU encoder (CCSDS 231.0-B coding/synchronization profile) out over the TC data link to the ground station. Every lifecycle stage - issued, queued, released, sent, acknowledged/verified - is persisted to the command history store, and verifiers close the loop from downlinked telemetry. Privileged issue options (constraint/verifier overrides) are gated by the authorization service and audited, denying non-elevated callers with an authorization error.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [command-issue.puml](command-issue.puml) | Command issue via the documented POST endpoint, with unknown-command and invalid-argument rejection branches | L1-CMD-001, L2-CMD-001 |
| [command-queue-inspect.puml](command-queue-inspect.puml) | Queue and queue-entry inspection via documented queue methods, with unknown-queue branch | L1-CMD-001, L2-CMD-002 |
| [command-queue-accept.puml](command-queue-accept.puml) | Accepting (releasing) a queued command for dispatch, with entry-not-found branch | L1-CMD-001, L2-CMD-002, L2-CMD-005 |
| [command-queue-reject.puml](command-queue-reject.puml) | Rejecting (discarding) a queued command, with entry-not-found branch | L1-CMD-001, L2-CMD-002, L2-CMD-005 |
| [cop1-nominal-transfer.puml](cop1-nominal-transfer.puml) | COP-1 AD-service initialization and nominal frame transfer with CLCW acknowledgement | L1-CMD-001, L2-CMD-003 |
| [cop1-retransmission.puml](cop1-retransmission.puml) | COP-1 retransmission on CLCW retransmit flag or timer expiry, and alert on transmission-limit exceeded | L1-CMD-001, L2-CMD-003 |
| [cltu-generation.puml](cltu-generation.puml) | CLTU generation (randomization, BCH codeblocks, start/tail sequences) from a validated TC frame, with oversize-frame rejection | L1-CMD-001, L2-CMD-004 |
| [command-history-lifecycle.puml](command-history-lifecycle.puml) | Persistence of each lifecycle stage and ordered history query | L1-CMD-002, L2-CMD-005 |
| [privileged-option-authorization.puml](privileged-option-authorization.puml) | Authorization check for privileged issue options: denial for non-elevated callers, audited use for elevated callers | L1-CMD-002, L2-CMD-006 |
| [command-verification.puml](command-verification.puml) | Telemetry-based command verification with success and check-window-timeout outcomes | L1-CMD-001, L2-CMD-005 |
| [command-end-to-end-dispatch.puml](command-end-to-end-dispatch.puml) | Full command path demonstration: issue, queue release, COP-1/CLTU dispatch, verification | L1-CMD-001, L2-CMD-001, L2-CMD-002, L2-CMD-003, L2-CMD-004, L2-CMD-005 |
