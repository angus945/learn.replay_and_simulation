using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Contract;

namespace SimulationCore.SimulationActor.Domain
{
    internal sealed class ActorPools
    {
        private readonly Dictionary<int, ActorPool> pools = new();
        private readonly Dictionary<int, Type> poolTypes = new();

        public void AddPool<T>(int poolId, int capacity) where T : class, IActor
        {
            if (pools.ContainsKey(poolId))
                throw new InvalidOperationException($"Pool {poolId} is already registered.");

            ActorPool pool = new ActorPool(poolId, capacity);

            pools.Add(pool.PoolId, pool);
            poolTypes.Add(pool.PoolId, typeof(T));
        }

        public ActorAcquireResult Acquire(int poolId)
        {
            if (!pools.TryGetValue(poolId, out ActorPool pool))
                throw new InvalidOperationException($"Pool {poolId} is not registered.");

            return pool.Acquire();
        }

        public int[] GetSortedPoolIds()
        {
            int[] poolIds = new int[pools.Count];
            pools.Keys.CopyTo(poolIds, 0);
            Array.Sort(poolIds);

            return poolIds;
        }

        public void Release(int archetypeId, int slotId)
        {
            if (!pools.TryGetValue(archetypeId, out ActorPool pool))
                throw new InvalidOperationException($"Pool {archetypeId} is not registered.");

            pool.Release(slotId);
        }
    }
}