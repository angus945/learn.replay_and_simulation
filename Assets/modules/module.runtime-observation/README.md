# module.runtime-observation

Host-published immutable runtime observations with explicit source, scope, epoch and capture identity.

`Read` never performs capture. A host publishes at its own safe consistency boundary, and readers can only read the latest or a specifically retained publication. Capture failures are retained as publications without replacing them with a partial observation.

The assembly targets `netstandard2.1`, uses C# 9, has no Unity, Simulation, Playback or I/O dependency, and keeps a bounded in-memory history.
