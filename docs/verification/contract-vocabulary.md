# Verification contract vocabulary

The verification capability family is frozen around one authoritative contract for each semantic concept.

| Term | Authoritative meaning | Owner |
|---|---|---|
| Fact | 已經發生且不可撤銷的事實描述 | `module.verification.system-fact` |
| Snapshot | 某個觀察邊界的 immutable state capture | `module.verification.state-snapshot` |
| Operation | 對 runtime 施加的可追蹤控制行為 | `module.verification.runtime-control` |
| Oracle | 對特定 expectation 的判定 | `module.verification.oracle` |
| Invariant | 對 committed state 永遠必須成立的規則 | `module.verification.invariant` |
| Diagnostic | 對值得注意條件的結構化解釋 | `module.verification.diagnostics` |
| Trace | 一段按順序保存的 payloads | `module.verification.trace-buffer` |
| Evidence | 對 verification artifacts 的 reference / manifest | `module.verification.evidence` |

`Observation`, `Result`, `Record`, `Event`, `State` and `Evidence` must not be introduced as cross-module umbrella names. A contract must state whether it is a fact, snapshot, operation, oracle judgment, invariant judgment, diagnostic, trace storage value or evidence reference.

The eight repositories and their public terminology are frozen. New requirements use adapters, projections or host composition. A breaking contract change requires an architecture reason; product-specific fields never enter a base verification module.

## Composition boundary

Base modules do not acquire family-wide dependencies merely because they share this vocabulary. RuntimeControl transitions become facts through a host adapter. System facts enter generic trace storage through a host observer. Invariant violations and diagnostics become facts only through an integration adapter. Evidence references retained artifacts without copying their payloads. Deterministic playback evidence remains owned by deterministic playback.

## Freeze status

All eight modules have one-sentence responsibilities, neutral contracts, explicit dependency direction, contract tests and README non-goals. Architecture checks in `tools/verify-architecture.ps1` lock repository names, namespaces, dependency isolation, product neutrality and retired duplicate DTO names.
