using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Contract;

namespace SimulationCore.SimulationActor.Domain
{
    public struct ActorAcquireResult
    {
        public bool HasActor { get; }
        public int SlotId { get; }
        public uint Generation { get; }

        public ActorAcquireResult(bool hasActor, int slotId, uint generation)
        {
            HasActor = hasActor;
            SlotId = slotId;
            Generation = generation;
        }
    }
    public sealed class ActorPool
    {
        private readonly ActorSlotState[] states;
        private readonly uint[] generations;
        private readonly SortedSet<int> freeActorIds;

        public int PoolId { get; }
        public int Capacity => states.Length;

        public ActorPool(int poolId, int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            PoolId = poolId;

            states = new ActorSlotState[capacity];
            generations = new uint[capacity];
            freeActorIds = new SortedSet<int>();

            for (int i = 0; i < capacity; i++)
            {
                freeActorIds.Add(i);
            }
        }

        public ActorAcquireResult Acquire()
        {
            if (freeActorIds.Count == 0)
            {
                return new ActorAcquireResult(false, -1, 0);
            }

            return AcquireAt(freeActorIds.Min);
        }
        public ActorAcquireResult AcquireAt(int slotId)
        {
            if (states[slotId] != ActorSlotState.Free)
            {
                throw new InvalidOperationException(
                    $"Resource {slotId} is not free.");
            }

            freeActorIds.Remove(slotId);

            generations[slotId]++;
            states[slotId] = ActorSlotState.Active;

            return new ActorAcquireResult(
                hasActor: true,
                slotId: slotId,
                generation: generations[slotId]);
        }

        public void Release(int slotId)
        {
            if (states[slotId] != ActorSlotState.Active)
            {
                throw new InvalidOperationException($"Resource {slotId} is not active.");
            }

            states[slotId] = ActorSlotState.Free;
            freeActorIds.Add(slotId);
        }



    }
}
