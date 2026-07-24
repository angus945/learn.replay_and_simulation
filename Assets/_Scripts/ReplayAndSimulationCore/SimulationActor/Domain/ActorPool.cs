using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Contract;

namespace SimulationCore.SimulationActor.Domain
{
    public interface IActorPool
    {
        int PoolId { get; }
        Type ActorType { get; }
        int Capacity { get; }

        void Release(int slotId);
    }
    public sealed class ActorPool<T> : IActorPool where T : class, IActor
    {
        private readonly T[] actors;
        private readonly ActorSlotState[] states;
        private readonly uint[] generations;
        private readonly SortedSet<int> freeActorIds;

        public int PoolId { get; }
        public Type ActorType => typeof(T);
        public int Capacity => actors.Length;

        public ActorPool(int poolId, T[] actors)
        {
            PoolId = poolId;

            this.actors = actors;

            states = new ActorSlotState[actors.Length];
            generations = new uint[actors.Length];
            freeActorIds = new SortedSet<int>();
        }

        public T Active()
        {
            if (freeActorIds.Count == 0)
            {
                throw new InvalidOperationException("No free actors available.");
            }

            return ActiveAt(freeActorIds.Min);
        }
        public T ActiveAt(int slotId)
        {
            if (states[slotId] != ActorSlotState.Free)
            {
                throw new InvalidOperationException(
                    $"Resource {slotId} is not free.");
            }

            freeActorIds.Remove(slotId);

            generations[slotId]++;
            states[slotId] = ActorSlotState.Active;

            T actor = actors[slotId];

            return actor;
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
