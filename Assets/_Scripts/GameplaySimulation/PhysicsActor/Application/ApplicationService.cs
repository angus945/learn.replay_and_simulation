using System;
using System.Collections.Generic;
using PhysicsActor;
using PhysicsActor.Application.Port;
using PhysicsActor.Contract;
using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    public sealed class PhysicsActorService
    {
        private readonly RegisterActorPoolUseCase registerActorPool;
        private readonly InitializeActorPoolsUseCase initializeActorPools;
        private readonly AcquireActorUseCase acquireActor;
        private readonly AcquireActorAtUseCase acquireActorAt;
        private readonly ActivatePendingActorsUseCase activatePendingActors;
        private readonly ReleaseActorUseCase releaseActor;

        public PhysicsActorService()
        {
            ActorPoolState state = new ActorPoolState();

            registerActorPool = new RegisterActorPoolUseCase(state);
            initializeActorPools = new InitializeActorPoolsUseCase(state);
            acquireActor = new AcquireActorUseCase(state);
            acquireActorAt = new AcquireActorAtUseCase(state);
            activatePendingActors = new ActivatePendingActorsUseCase(state);
            releaseActor = new ReleaseActorUseCase(state);
        }

        public void RegisterActorPool<T>(int poolId, int capacity, IActorFactory<T> factory) where T : class, IPhysicalActor
        {
            registerActorPool.Execute(poolId, capacity, factory);
        }

        public void InitializeActorPools()
        {
            initializeActorPools.Execute();
        }

        public ActorHandle AcquireActor(AcquireActorCommand command)
        {
            return acquireActor.Execute(command);
        }

        public ActorHandle AcquireActorAt(AcquireActorAtCommand command)
        {
            return acquireActorAt.Execute(command);
        }

        public void ActivatePendingActors(ActivatePendingActorsCommand command)
        {
            activatePendingActors.Execute(command);
        }

        public void ReleaseActor(ReleaseActorCommand command)
        {
            releaseActor.Execute(command);
        }
    }

    internal sealed class ActorPoolState
    {
        private readonly Dictionary<int, IActorPool> pools = new();

        public List<ActorHandle> PendingActivations { get; } = new();

        public void AddPool(IActorPool pool)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            if (pools.ContainsKey(pool.PoolId))
                throw new InvalidOperationException($"Pool {pool.PoolId} is already registered.");

            pools.Add(pool.PoolId, pool);
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

        public int[] GetSortedPoolIds()
        {
            int[] poolIds = new int[pools.Count];
            pools.Keys.CopyTo(poolIds, 0);
            Array.Sort(poolIds);

            return poolIds;
        }
    }
}
