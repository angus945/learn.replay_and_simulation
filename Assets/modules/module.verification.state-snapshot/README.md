# module.verification.state-snapshot

Host-published immutable state snapshots with explicit source, scope, epoch and capture identity.

`Read` never performs capture. A host publishes at its own safe consistency boundary, and readers can only read the latest or a specifically retained snapshot. Capture failures are retained without replacing them with a partial snapshot.

Public vocabulary is `StateSnapshotChannel`, `StateSnapshotReference`, `StateSnapshotRead<TSnapshot>`, `IStateSnapshotReader<TSnapshot>` and `IStateSnapshotPublisher<TSnapshot>`. This module does not define facts or event metadata.

The assembly targets `netstandard2.1`, uses C# 9, has no Unity, Simulation, Playback or I/O dependency, and keeps a bounded in-memory history.
