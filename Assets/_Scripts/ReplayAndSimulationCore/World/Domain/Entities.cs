using System;
using System.Collections.Generic;
using SimulationCore.World.Contract;

namespace SimulationCore.World.Domain
{
    public enum EntityState : byte
    {
        Free = 0,
        Alive = 1,
        Destroyed = 2
    }
    public struct Entity
    {
        public readonly int SlotId;
        public EntityState State;
        public ulong SequenceId;

        public Entity(int slotId) : this()
        {
            SlotId = slotId;
            State = EntityState.Free;
            SequenceId = 0;
        }
    }
    public class Entities
    {
        private readonly Entity[] entities;
        private readonly SortedSet<int> freeEntityIds = new();
        private readonly Dictionary<ulong, int> aliveEntityIdsBySpawnSequence = new();
        private readonly List<ulong> aliveSequenceIds = new();
        private ulong nextSpawnSequence = 1;

        public int Capacity => entities.Length;
        public int AliveEntityCount => aliveSequenceIds.Count;

        public Entities(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            entities = new Entity[capacity];

            for (int id = 0; id < capacity; id++)
            {
                entities[id] = new Entity(id);
                freeEntityIds.Add(id);
            }
        }
        public Entity Create()
        {
            if (freeEntityIds.Count == 0)
                throw new InvalidOperationException("Entity capacity exhausted.");

            int id = freeEntityIds.Min;
            freeEntityIds.Remove(id);

            ulong spawnSequence = nextSpawnSequence;
            if (spawnSequence == 0)
                throw new InvalidOperationException("Entity spawn sequence overflow.");

            nextSpawnSequence++;
            entities[id].State = EntityState.Alive;
            entities[id].SequenceId = spawnSequence;
            aliveEntityIdsBySpawnSequence[spawnSequence] = id;
            aliveSequenceIds.Add(spawnSequence);

            return entities[id];
        }
        public bool IsAlive(int slotId, ulong sequenceId)
        {
            if (slotId < 0 || slotId >= entities.Length)
                return false;

            Entity e = entities[slotId];
            return e.State == EntityState.Alive && e.SequenceId == sequenceId;
        }
        public Entity GetAliveEntityBySpawnSequence(int index)
        {
            if ((uint)index >= (uint)aliveSequenceIds.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            ulong spawnSequence = aliveSequenceIds[index];
            int slotId = aliveEntityIdsBySpawnSequence[spawnSequence];

            return entities[slotId];
        }

        internal Entity GetEntity(int slotId, ulong sequenceId)
        {
            if (slotId < 0 || slotId >= entities.Length)
                throw new ArgumentOutOfRangeException(nameof(slotId));

            Entity e = entities[slotId];
            if (e.SequenceId != sequenceId)
                throw new InvalidOperationException("Entity sequence ID does not match.");

            return e;
        }

        internal void MarkForDestroy(int slotId, ulong sequenceId)
        {
            if (!IsAlive(slotId, sequenceId))
                throw new InvalidOperationException("Entity is not alive.");

            entities[slotId].State = EntityState.Destroyed;
            freeEntityIds.Add(slotId);
            aliveEntityIdsBySpawnSequence.Remove(sequenceId);
            aliveSequenceIds.Remove(sequenceId);
        }
        internal void CommitDestroy(int slotId, ulong sequenceId)
        {
            if (!IsPendingDestroy(slotId, sequenceId))
                throw new InvalidOperationException("Entity is not pending destroy.");

            entities[slotId].State = EntityState.Free;
            entities[slotId].SequenceId = 0;
        }
        bool IsPendingDestroy(int slotId, ulong sequenceId)
        {
            if (slotId < 0 || slotId >= entities.Length)
                throw new ArgumentOutOfRangeException(nameof(slotId));

            Entity e = entities[slotId];
            return e.State == EntityState.Destroyed && e.SequenceId == sequenceId;
        }
    }
}