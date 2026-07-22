using System;
using System.Collections.Generic;

public sealed class ActorPoolRegistry
{
    private readonly Dictionary<int, IActorPool> pools = new();

    public void Register(IActorPool pool)
    {
        if (pool == null)
            throw new ArgumentNullException(nameof(pool));

        if (!pools.TryAdd(pool.PoolId, pool))
        {
            throw new InvalidOperationException(
                $"Pool {pool.PoolId} is already registered.");
        }
    }

    public IActorPool GetPool(int poolId)
    {
        if (!pools.TryGetValue(poolId, out IActorPool pool))
        {
            throw new InvalidOperationException(
                $"Pool {poolId} is not registered.");
        }

        return pool;
    }
}