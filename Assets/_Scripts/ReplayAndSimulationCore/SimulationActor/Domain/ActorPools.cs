using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Contract;

namespace SimulationCore.SimulationActor.Domain
{
    internal sealed class ActorPools
    {
        private readonly Dictionary<int, IActorPool> pools = new();
        private readonly Dictionary<int, Type> poolTypes = new();

        public void AddPool<T>(IActorPool pool) where T : IActor
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            if (pools.ContainsKey(pool.PoolId))
                throw new InvalidOperationException($"Pool {pool.PoolId} is already registered.");

            pools.Add(pool.PoolId, pool);
            poolTypes.Add(pool.PoolId, typeof(T));
        }

        public T GetPool<T>(int poolId) where T : IActorPool
        {
            if (!pools.TryGetValue(poolId, out IActorPool pool))
            {
                throw new InvalidOperationException(
                    $"Pool {poolId} is not registered.");
            }

            return (T)pool;
        }

        public int[] GetSortedPoolIds()
        {
            int[] poolIds = new int[pools.Count];
            pools.Keys.CopyTo(poolIds, 0);
            Array.Sort(poolIds);

            return poolIds;
        }
    }
}