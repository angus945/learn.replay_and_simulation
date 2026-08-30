using System;
using System.Collections.Generic;
using SimulationObjects.Contract;

namespace SimulationObjects
{
    /// <summary>
    /// Single-threaded identity/lifecycle mechanism. Contains no BC state, components or Unity instances.
    /// Pending destroys remain active until Commit; slots are reusable only after Commit.
    /// </summary>
    public sealed class SimulationObjectRegistry : ISimulationObjectRegistry
    {
        private sealed class Slot
        {
            internal uint Generation = 1;
            internal SimulationObjectRecord Record;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly SortedSet<int> freeSlots = new SortedSet<int>();
        private readonly Dictionary<SimulationObjectId, int> slotsById = new Dictionary<SimulationObjectId, int>();
        private readonly int maxCapacity;
        private ulong lastObjectId;
        private ulong lastSpawnSequence;

        public SimulationObjectRegistry(int maxCapacity = int.MaxValue)
        {
            if (maxCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            this.maxCapacity = maxCapacity;
        }

        public SimulationObjectRecord RequestSpawn()
        {
            if (lastObjectId == ulong.MaxValue)
                throw new InvalidOperationException("Simulation object ID space exhausted.");
            if (freeSlots.Count == 0 && slots.Count >= maxCapacity)
                throw new InvalidOperationException("Registry capacity exhausted; commit pending destroys first.");

            int index;
            if (freeSlots.Count > 0)
            {
                index = freeSlots.Min;
                freeSlots.Remove(index);
            }
            else
            {
                index = slots.Count;
                slots.Add(new Slot());
            }

            Slot slot = slots[index];
            var id = new SimulationObjectId(++lastObjectId);
            var handle = new SimulationObjectHandle(index, slot.Generation);
            slot.Record = new SimulationObjectRecord(id, handle, 0, SimulationObjectState.PendingSpawn);
            slotsById.Add(id, index);
            return slot.Record;
        }

        /// <returns>False if already pending destroy. Stale/invalid handles throw.</returns>
        public bool RequestDestroy(SimulationObjectHandle handle)
        {
            if (!TryGet(handle, out SimulationObjectRecord record))
                throw new InvalidOperationException($"Unknown or stale handle {handle}.");
            if (record.State == SimulationObjectState.PendingDestroy) return false;

            slots[handle.Slot].Record = new SimulationObjectRecord(record.Id, handle,
                record.SpawnSequence, SimulationObjectState.PendingDestroy);
            return true;
        }

        public bool TryGet(SimulationObjectHandle handle, out SimulationObjectRecord record)
        {
            record = default;
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= slots.Count) return false;
            Slot slot = slots[handle.Slot];
            if (slot.Generation != handle.Generation || !slot.Record.Id.IsValid) return false;
            record = slot.Record;
            return true;
        }

        public bool TryGet(SimulationObjectId id, out SimulationObjectRecord record)
        {
            record = default;
            if (!slotsById.TryGetValue(id, out int index)) return false;
            record = slots[index].Record;
            return true;
        }

        /// <summary>Alive plus committed PendingDestroy objects, ordered by SpawnSequence.</summary>
        public IReadOnlyList<SimulationObjectRecord> GetActiveOrdered()
        {
            var result = new List<SimulationObjectRecord>();
            foreach (Slot slot in slots)
                if (slot.Record.IsActive) result.Add(slot.Record);
            result.Sort((left, right) => left.SpawnSequence.CompareTo(right.SpawnSequence));
            return result.AsReadOnly();
        }

        /// <summary>All reserved objects, including pending changes, ordered by stable ID.</summary>
        public IReadOnlyList<SimulationObjectRecord> GetObjectsOrdered()
        {
            return GetOrderedRecords().AsReadOnly();
        }

        public StructuralCommitResult Commit()
        {
            List<SimulationObjectRecord> ordered = GetOrderedRecords();
            ulong spawnCount = 0;
            foreach (var record in ordered)
                if (record.State == SimulationObjectState.PendingSpawn) spawnCount++;
            if (spawnCount > ulong.MaxValue - lastSpawnSequence)
                throw new InvalidOperationException("Spawn sequence space exhausted.");

            var spawned = new List<SimulationObjectRecord>();
            var destroyed = new List<SimulationObjectRecord>();
            var cancelled = new List<SimulationObjectRecord>();

            foreach (var record in ordered)
            {
                if (record.State != SimulationObjectState.PendingDestroy) continue;
                if (record.SpawnSequence == 0) cancelled.Add(record);
                else destroyed.Add(record);
                Release(record);
            }

            foreach (var record in ordered)
            {
                if (record.State != SimulationObjectState.PendingSpawn) continue;
                var alive = new SimulationObjectRecord(record.Id, record.Handle,
                    ++lastSpawnSequence, SimulationObjectState.Alive);
                slots[record.Handle.Slot].Record = alive;
                spawned.Add(alive);
            }

            return new StructuralCommitResult(spawned.ToArray(), destroyed.ToArray(), cancelled.ToArray());
        }

        private List<SimulationObjectRecord> GetOrderedRecords()
        {
            var result = new List<SimulationObjectRecord>();
            foreach (Slot slot in slots)
                if (slot.Record.Id.IsValid) result.Add(slot.Record);
            result.Sort((left, right) => left.Id.CompareTo(right.Id));
            return result;
        }

        private void Release(SimulationObjectRecord record)
        {
            int index = record.Handle.Slot;
            Slot slot = slots[index];
            slotsById.Remove(record.Id);
            slot.Record = default;

            // Retire the slot rather than wrap generation and revive a stale reference.
            if (slot.Generation == uint.MaxValue) return;
            slot.Generation++;
            freeSlots.Add(index);
        }
    }
}
