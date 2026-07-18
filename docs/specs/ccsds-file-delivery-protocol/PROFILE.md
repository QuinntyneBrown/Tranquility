# CFDP baseline profile TRQ-CFDP-BP1

Declared baseline CCSDS File Delivery Protocol profile required by
**L2-FDP-004** (resolves TBD-008). Every CFDP conformance test references this
single profile id (`TRQ-CFDP-BP1`), enforced by `CfdpProfileDeclarationTests`.

Revision: 1 (2026-07-17).

| Aspect | Baseline choice |
|---|---|
| Classes | Class 1 (unacknowledged) and Class 2 (acknowledged, deferred NAK) |
| Checksum | Modular checksum, type 0 (CCSDS 727.0-B) |
| File segment size | 1024 octets |
| Entity IDs | 2 octets |
| Transaction sequence number | 4 octets |
| PDU CRC | Off |
| PDU set | Metadata, File Data, EOF, Finished, ACK, NAK |
| EOF-ACK timer | 10 s |
| NAK timer | 10 s |
| Inactivity timer | 300 s |
| ACK / NAK limit | 3 |
| Fault handling | Cancel the transaction |

Out of baseline scope: Keep-Alive, Prompt, proxy/directory operations, the
large-file flag, and store-and-forward overlay.
