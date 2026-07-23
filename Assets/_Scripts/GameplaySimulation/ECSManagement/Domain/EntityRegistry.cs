using System;
using System.Collections.Generic;
using ECSManagement.Contract;

namespace ECSManagement.Domain
{
    internal sealed class EntityRegistry
    {
        private readonly EntitySlotState[] states;
        private readonly uint[] generations;
        private readonly ulong[] spawnSequences;
        private readonly SortedSet<int> freeEntityIds = new();
        private readonly Dictionary<ulong, int> aliveEntityIdsBySpawnSequence;
        private readonly List<ulong> aliveSpawnSequences;
        private ulong nextSpawnSequence = 1;

        public int Capacity => states.Length;
        public int AliveEntityCount => aliveSpawnSequences.Count;

        public EntityRegistry(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            states = new EntitySlotState[capacity];
            generations = new uint[capacity];
            spawnSequences = new ulong[capacity];
            aliveEntityIdsBySpawnSequence = new Dictionary<ulong, int>(capacity);
            aliveSpawnSequences = new List<ulong>(capacity);

            for (int id = 0; id < capacity; id++)
            {
                states[id] = EntitySlotState.Free;
                freeEntityIds.Add(id);
            }
        }

        public EntityHandle Reserve()
        {
            if (freeEntityIds.Count == 0)
                throw new InvalidOperationException("Entity capacity exhausted.");

            int id = freeEntityIds.Min;
            freeEntityIds.Remove(id);

            uint generation = generations[id] + 1;
            if (generation == 0)
            {
                throw new InvalidOperationException(
                    $"Entity generation overflow at slot {id}.");
            }

            ulong spawnSequence = nextSpawnSequence;
            if (spawnSequence == 0)
                throw new InvalidOperationException("Entity spawn sequence overflow.");

            nextSpawnSequence++;
            generations[id] = generation;
            spawnSequences[id] = spawnSequence;
            states[id] = EntitySlotState.Reserved;

            return new EntityHandle(id, generation, spawnSequence);
        }

        public void CommitCreate(EntityHandle entity)
        {
            Validate(entity, EntitySlotState.Reserved);
            aliveEntityIdsBySpawnSequence.Add(entity.SpawnSequence, entity.Id);
            InsertAliveSpawnSequence(entity.SpawnSequence);
            states[entity.Id] = EntitySlotState.Alive;
        }

        public void AbortCreate(EntityHandle entity)
        {
            Validate(entity, EntitySlotState.Reserved);

            states[entity.Id] = EntitySlotState.Free;
            freeEntityIds.Add(entity.Id);
        }

        public void MarkDestroy(EntityHandle entity)
        {
            Validate(entity, EntitySlotState.Alive);
            if (!aliveEntityIdsBySpawnSequence.Remove(entity.SpawnSequence))
                throw new InvalidOperationException("Alive entity index is missing spawn sequence.");

            RemoveAliveSpawnSequence(entity.SpawnSequence);
            states[entity.Id] = EntitySlotState.PendingDestroy;
        }

        public bool IsAlive(EntityHandle entity)
        {
            if ((uint)entity.Id >= (uint)states.Length)
                return false;

            return generations[entity.Id] == entity.Generation &&
                   spawnSequences[entity.Id] == entity.SpawnSequence &&
                   states[entity.Id] == EntitySlotState.Alive;
        }

        public bool TryGetAliveEntity(int entityId, out EntityHandle entity)
        {
            entity = default;

            if ((uint)entityId >= (uint)states.Length)
                return false;

            if (states[entityId] != EntitySlotState.Alive)
                return false;

            entity = new EntityHandle(
                entityId,
                generations[entityId],
                spawnSequences[entityId]);
            return true;
        }

        public bool TryGetAliveEntityBySpawnSequence(ulong spawnSequence, out EntityHandle entity)
        {
            entity = default;

            if (!aliveEntityIdsBySpawnSequence.TryGetValue(
                    spawnSequence,
                    out int entityId))
            {
                return false;
            }

            return TryGetAliveEntity(entityId, out entity);
        }

        public EntityHandle GetAliveEntityBySpawnSequenceIndex(int index)
        {
            if ((uint)index >= (uint)aliveSpawnSequences.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            ulong spawnSequence = aliveSpawnSequences[index];
            int entityId = aliveEntityIdsBySpawnSequence[spawnSequence];
            if (TryGetAliveEntity(entityId, out EntityHandle entity))
                return entity;

            throw new InvalidOperationException(
                "Spawn sequence index contains a stale entity.");
        }

        internal void CommitDestroy(EntityHandle entity)
        {
            Validate(entity, EntitySlotState.PendingDestroy);

            states[entity.Id] = EntitySlotState.Free;
            freeEntityIds.Add(entity.Id);
        }

        internal bool CanBuild(EntityHandle entity)
        {
            if ((uint)entity.Id >= (uint)states.Length)
                return false;

            if (generations[entity.Id] != entity.Generation)
                return false;

            if (spawnSequences[entity.Id] != entity.SpawnSequence)
                return false;

            EntitySlotState state = states[entity.Id];

            return state == EntitySlotState.Reserved ||
                   state == EntitySlotState.Alive;
        }

        private void Validate(EntityHandle entity, EntitySlotState expectedState)
        {
            if ((uint)entity.Id >= (uint)states.Length)
                throw new InvalidOperationException("Invalid entity ID.");

            if (generations[entity.Id] != entity.Generation)
                throw new InvalidOperationException("Stale entity handle.");

            if (spawnSequences[entity.Id] != entity.SpawnSequence)
                throw new InvalidOperationException("Stale entity spawn sequence.");

            if (states[entity.Id] != expectedState)
            {
                throw new InvalidOperationException(
                    $"Expected entity state {expectedState}, but found {states[entity.Id]}.");
            }
        }

        private void InsertAliveSpawnSequence(ulong spawnSequence)
        {
            int index = aliveSpawnSequences.BinarySearch(spawnSequence);
            if (index >= 0)
                throw new InvalidOperationException("Duplicate entity spawn sequence.");

            aliveSpawnSequences.Insert(~index, spawnSequence);
        }

        private void RemoveAliveSpawnSequence(ulong spawnSequence)
        {
            int index = aliveSpawnSequences.BinarySearch(spawnSequence);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "Alive entity sorted index is missing spawn sequence.");
            }

            aliveSpawnSequences.RemoveAt(index);
        }
    }
}
