# TRQ-ICD-API

## 1. Purpose

This interface control document defines the external API compatibility target for Tranquility.

## 2. Compatibility target

1. Tranquility targets wire compatibility with the documented reference mission-control HTTP and WebSocket API.
2. Compatibility is derived from published documentation only.
3. Reference mission-control source code is out of bounds for derivation.

**Primary sources**

- External API documentation package (URL redacted)
- External API documentation package (URL redacted)
- External API documentation package (URL redacted)

## 3. Baseline protocol behavior

| Area | Required behavior | Source |
|---|---|---|
| HTTP verbs | Resource operations use documented GET/POST/PATCH/DELETE semantics. | External API documentation package (URL redacted) |
| Error envelope | Non-2xx responses expose `{"exception":{"type": "...","msg":"..."}}`. | External API documentation package (URL redacted) |
| Time format | API timestamps use UTC ISO 8601 / RFC 3339. | External API documentation package (URL redacted) |
| JSON/Protobuf | Endpoints support documented JSON behavior and documented Protobuf content negotiation where published. | External API documentation package (URL redacted) |
| WebSocket endpoint | Subscription API exposed on `/api/websocket`. | External API documentation package (URL redacted) |
| WebSocket protocol | JSON and Protobuf subprotocols, call correlation, sequence tracking, cancel/state built-ins. | External API documentation package (URL redacted) |

## 4. Baseline resource families in scope

| Family | Baseline examples (documented behavior) | Source |
|---|---|---|
| Instances | `GET /api/instances`, `GET /api/instances/{instance}` | External API documentation package (URL redacted), External API documentation package (URL redacted) |
| Processing | `GET /api/processors`, create/edit/delete/list processor methods | External API documentation package (URL redacted) |
| Links | `GET /api/links/{instance}`, enable/disable/reset/subscribe methods | External API documentation package (URL redacted) |
| Commands | `POST /api/processors/{instance}/{processor}/commands/{name}`, command retrieval/subscription methods | External API documentation package (URL redacted), External API documentation package (URL redacted) |
| Queues | `GET /api/processors/{instance}/{processor}/queues`, accept/reject operations | External API documentation package (URL redacted), External API documentation package (URL redacted) |
| Parameter history/archive | `GET /api/archive/{instance}/parameters/{name}`, archive segment queries | External API documentation package (URL redacted), External API documentation package (URL redacted) |
| Streaming replay | `POST /api/archive/{instance}:streamParameterValues` | External API documentation package (URL redacted) |
| Time correlation | `GET /api/tco/{instance}/{serviceName}/status` and related methods | External API documentation package (URL redacted) |
| IAM | users/groups/roles/service-account methods | External API documentation package (URL redacted) |
| File transfer (CFDP service integration) | `/api/filetransfer/{instance}/{serviceName}/transfers` and related lifecycle operations | External API documentation package (URL redacted) |

## 5. Compatibility boundaries

1. This ICD constrains externally observable behavior only.
2. Internal architecture and storage implementation are not part of this ICD.
3. Any deliberate compatibility divergence requires:
   - documented justification,
   - explicit requirement reference,
   - differential harness evidence.

## 6. Blocking legal escalation

The handling of reference mission-control system `.proto` interface definitions is a blocking legal decision and is not resolved in this ICD.

Open question `TBD-012` remains unresolved and requires a product/legal decision.

Neutral options under review:

1. Reimplement from published prose docs only.
2. Consume published `.proto` definitions as interface descriptions.
3. Define native Tranquility API and provide a compatibility shim.
4. Seek a licensing arrangement with Space Applications Services.
