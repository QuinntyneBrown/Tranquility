# Development conventions

## ATDD: RED → GREEN milestones

All product behaviour is driven by the traced acceptance suite in
`tests/Tranquility.AcceptanceTests`. Every acceptance test carries one or more
`[Requirement("L2-XXX-NNN")]` attributes binding it to a requirement in
`docs/specs/<subsystem>/L2.md`.

Per milestone, on a branch `milestone/mN-<name>`:

1. **RED commit** (`RED: ...`): add the milestone's failing acceptance tests
   and delete the requirement IDs they cover from
   `tests/Tranquility.AcceptanceTests/Traceability/CoverageBaseline.json`.
   CI still runs and uploads the failing trx (`if: always()`) — that recorded
   run is the failing-first evidence. The RED commit is the executable
   definition of milestone scope.
2. **GREEN commits** (`GREEN: ...`): implement until the suite passes.
   Adapting proven code from git history (`git show <sha>:<path>`) is
   encouraged; bulk restores are not.
3. Merge to `main` only when the full suite is green. `main` is never red.

## Traceability meta-tests (always on)

- Every L2 ID is either covered by a traced test or listed in
  `CoverageBaseline.json` — nothing is silently dropped.
- **Ratchet**: an ID with a test must leave the baseline in the same commit;
  coverage only moves forward.
- `[Requirement]` may only reference IDs that exist in `docs/specs`.
- When `CoverageBaseline.json` is deleted (M11), the final gate arms: all 67
  L2 IDs must carry at least one passing acceptance test, forever.

## Test taxonomy

| Project | Role |
|---|---|
| `Tranquility.AcceptanceTests` | The traced ATDD suite. Also hosts architecture-conformance, determinism, metadata, scope-guard, and license-gate tests. The only assembly where `[Requirement]` lives. |
| `Tranquility.Core.Tests` etc. | Untraced unit tests and golden vectors supporting the implementation. |
| `Tranquility.Benchmarks` | Performance verification against `thresholds.json` (numbers mirror `docs/PERFORMANCE-BASELINE.md`). |

## Determinism rules

- `Tranquility.Core` has zero I/O, zero clock reads, zero package
  dependencies (enforced by purity tests, L2-QLT-002). Protocol engines are
  pure mailbox automata: time arrives as events, timers are outputs.
- No `Thread.Sleep` in tests: use `TimeProvider` fakes and bounded
  deadline-polling helpers.

## Platform

net10.0; Linux-first (CI on ubuntu-latest is the merge gate; a nightly
Windows job keeps the dev platform honest). Dependencies must be
Apache-2.0-compatible and are license-audited in CI (see
`Directory.Packages.props` for the pinned, annotated set).
