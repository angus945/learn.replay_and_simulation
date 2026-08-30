using System;
using System.Collections.Generic;
using Arena.Application;
using Arena.Domain;
using SimulationObjects;
using SimulationObjects.Contract;

namespace Arena.Infrastructure
{
    /// <summary>Maps game identity to registry identity; only Commit removes repository entries.</summary>
    public sealed class RegistryLifecycle : IActorLifecycle
    {
        private readonly IActorRepository repository;
        private readonly SimulationObjectRegistry registry;
        private readonly SortedDictionary<ActorId, SimulationObjectId> bindings = new SortedDictionary<ActorId, SimulationObjectId>();
        private readonly SortedDictionary<int, uint> freeGenerations = new SortedDictionary<int, uint>();
        private ulong allocated;
        private ulong committedSpawns;
        public RegistryLifecycle(IActorRepository repository, int capacity = 64)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            registry = new SimulationObjectRegistry(capacity);
        }
        public void Spawn(Actor actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (bindings.ContainsKey(actor.Id)) throw new InvalidOperationException("Duplicate actor identity.");
            SimulationObjectRecord record = registry.RequestSpawn();
            allocated++;
            freeGenerations.Remove(record.Handle.Slot);
            bindings.Add(actor.Id, record.Id);
            repository.Add(actor);
        }
        public void Despawn(ActorId id)
        {
            if (!bindings.TryGetValue(id, out SimulationObjectId simulationId)) return;
            if (registry.TryGet(simulationId, out SimulationObjectRecord record)) registry.RequestDestroy(record.Handle);
        }
        public bool IsActive(ActorId id) => bindings.TryGetValue(id, out SimulationObjectId simulationId)
            && registry.TryGet(simulationId, out SimulationObjectRecord record) && record.IsActive;
        public int ActiveCount => registry.GetActiveOrdered().Count;
        public void Commit()
        {
            StructuralCommitResult changes = registry.Commit();
            committedSpawns += (ulong)changes.Spawned.Count;
            List<ActorId> removed = new List<ActorId>();
            foreach (KeyValuePair<ActorId, SimulationObjectId> pair in bindings)
                if (!registry.TryGet(pair.Value, out SimulationObjectRecord ignored)) removed.Add(pair.Key);
            foreach (ActorId id in removed) { bindings.Remove(id); repository.Remove(id); }
            foreach (SimulationObjectRecord record in changes.Destroyed)
                freeGenerations[record.Handle.Slot] = record.Handle.Generation == uint.MaxValue ? uint.MaxValue : record.Handle.Generation + 1;
            foreach (SimulationObjectRecord record in changes.CancelledSpawns)
                freeGenerations[record.Handle.Slot] = record.Handle.Generation == uint.MaxValue ? uint.MaxValue : record.Handle.Generation + 1;
        }
        /// <summary>Detached allocator/binding evidence, not a restore format. Stable order is explicit.</summary>
        public IReadOnlyList<ulong> CaptureEvidence()
        {
            List<ulong> values = new List<ulong> { allocated, committedSpawns, (ulong)bindings.Count };
            foreach (KeyValuePair<ActorId, SimulationObjectId> pair in bindings)
            {
                if (!registry.TryGet(pair.Value, out SimulationObjectRecord record)) throw new InvalidOperationException("Missing registry binding.");
                values.Add(pair.Key.Value); values.Add(record.Id.Value); values.Add((ulong)record.Handle.Slot);
                values.Add(record.Handle.Generation); values.Add(record.SpawnSequence); values.Add((ulong)record.State);
            }
            values.Add((ulong)freeGenerations.Count);
            foreach (KeyValuePair<int, uint> pair in freeGenerations) { values.Add((ulong)pair.Key); values.Add(pair.Value); }
            return values.AsReadOnly();
        }
    }
}
