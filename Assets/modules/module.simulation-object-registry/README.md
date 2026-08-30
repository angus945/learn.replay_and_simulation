# module.simulation-object-registry

Single-threaded, framework/engine-independent identity and structural lifecycle.

- One registry per simulation session. IDs/handles are scoped to that registry.
- RequestSpawn reserves a unique nonzero ID and lowest free slot immediately.
  It remains PendingSpawn (SpawnSequence zero) until Commit.
- RequestDestroy is idempotent for an already-pending destroy; stale handles throw.
  Committed PendingDestroy objects remain in GetActiveOrdered until Commit.
- Commit removes pending destroys/cancelled spawns first, then makes pending spawns Alive.
  Each result list is ordered by object ID, independent of destroy request order.
- Destroy before the first commit cancels a spawn: no Alive transition, no spawn sequence.
- Slots become reusable only after Commit. Generation increments; exhausted generations
  retire slots rather than wrap. IDs and committed spawn sequences never wrap/reuse.
- Capacity includes pending reservations and retired slots. It does not reuse slots scheduled
  for destruction before a commit.
- GetActiveOrdered returns committed objects by SpawnSequence; GetObjectsOrdered includes
  pending states by ID. Returned records/collections are immutable observations.
- Commit results are for the host to coordinate BC cleanup and Unity binding.
  The registry never invokes callbacks or decides phase timing.
- No ECS stores, Aggregate data, Unity instances, snapshot restore, or state hash algorithm.
  Ordered observations provide a traversal boundary, not a full restorable checkpoint
  (free-slot generations and allocation counters would also be required).

## Minimal usage

```csharp
SimulationObjects.SimulationObjectRegistry objects = new SimulationObjects.SimulationObjectRegistry();
SimulationObjects.Contract.SimulationObjectRecord pending = objects.RequestSpawn();
SimulationObjects.Contract.StructuralCommitResult born = objects.Commit(); // host chooses the structural boundary
// Host adapters create/bind presentation instances for born.Spawned.
objects.RequestDestroy(pending.Handle);
SimulationObjects.Contract.StructuralCommitResult removed = objects.Commit();
// Host adapters unbind removed.Destroyed; the old handle is now invalid.
```
