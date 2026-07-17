# Security — detailed design

The Security (SEC) subsystem enforces the Tranquility server's access-control and accountability posture across every API surface. An ASP.NET Core authentication middleware establishes caller identity (bearer tokens issued by a Token Service backed by the IAM Store), an Authorization Service resolves role/privilege grants before any privileged control method (command issue, queue action, link control) reaches its Application-layer handler, and an IAM Handler exposes documented resource operations for users, groups, roles, and service accounts. Every authentication event, authorization denial, and command uplink action is appended by the Audit Logger to an immutable, append-only Audit Log queryable over the HTTP API, and all HTTP and WebSocket transport terminates on a Kestrel TLS endpoint in production so that no credential, command, or telemetry traverses the network in cleartext.

| Diagram | Behaviour | Requirements |
|---|---|---|
| [auth-token-issue.puml](auth-token-issue.puml) | Bearer token issuance with credential validation; invalid-credential rejection; auth events audited | L1-SEC-001, L2-SEC-002, L2-SEC-004 |
| [unauthenticated-mutation-rejected.puml](unauthenticated-mutation-rejected.puml) | State-changing API request without authenticated identity is rejected with 401 and audited | L1-SEC-001, L2-SEC-002, L2-SEC-004 |
| [iam-user-management.puml](iam-user-management.puml) | IAM user resource operations: list, create, update per documented resource structures | L1-SEC-001, L2-SEC-001 |
| [iam-group-management.puml](iam-group-management.puml) | IAM group resource operations: list, create, update (membership changes) | L1-SEC-001, L2-SEC-001 |
| [iam-role-management.puml](iam-role-management.puml) | IAM role resource operations: list, create, update (privilege set changes) | L1-SEC-001, L2-SEC-001 |
| [iam-service-account-management.puml](iam-service-account-management.puml) | IAM service account operations: list, create (one-time credential issue), update | L1-SEC-001, L2-SEC-001 |
| [command-issue-authorization.puml](command-issue-authorization.puml) | Command issue with privilege check: authorized issue audited as command uplink; denial returns 403 and is audited | L1-SEC-001, L2-SEC-003, L2-SEC-004 |
| [queue-action-authorization.puml](queue-action-authorization.puml) | Command queue action (enable/disable/block) with privilege check; grant and denial paths audited | L1-SEC-001, L2-SEC-003, L2-SEC-004 |
| [link-control-authorization.puml](link-control-authorization.puml) | Link control (enable/disable) with privilege check; grant and denial paths audited | L1-SEC-001, L2-SEC-003, L2-SEC-004 |
| [audit-query.puml](audit-query.puml) | Audit trail query returning immutable records with actor, action, timestamp, outcome; unauthorized query denied | L1-SEC-001, L1-SEC-002, L2-SEC-004 |
| [tls-http-transport.puml](tls-http-transport.puml) | TLS handshake and HTTPS request flow for the HTTP API; cleartext attempt refused in production | L1-SEC-002, L2-SEC-005 |
| [tls-websocket-transport.puml](tls-websocket-transport.puml) | TLS handshake, wss upgrade, and encrypted subscription traffic for the WebSocket API; cleartext refused | L1-SEC-002, L2-SEC-005 |
