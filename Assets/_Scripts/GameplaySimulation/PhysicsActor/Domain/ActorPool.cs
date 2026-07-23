using System;
using System.Collections.Generic;
using PhysicsActor;
using PhysicsActor.Application.Port;
using PhysicsActor.Contract;

namespace PhysicsActor.Domain
{
    public sealed class ActorPool<T> : IActorPool where T : class, IPhysicalActor
    {
        private readonly IActorFactory<T> factory;
        private readonly T[] actors;
        private readonly ActorSlotState[] states;
        private readonly uint[] generations;
        private readonly SortedSet<int> freeActorIds;

        private bool initialized;

        public int PoolId { get; }
        public Type ActorType => typeof(T);
        public int Capacity => actors.Length;

        public ActorPool(int poolId, IActorFactory<T> factory, int capacity)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            PoolId = poolId;
            this.factory = factory;

            actors = new T[capacity];
            states = new ActorSlotState[capacity];
            generations = new uint[capacity];
            freeActorIds = new SortedSet<int>();
        }

        public void Initialize()
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Actor pool is already initialized.");
            }

            for (int actorId = 0; actorId < actors.Length; actorId++)
            {
                T actor = factory.CreateActor()
                    ?? throw new InvalidOperationException(
                        $"Factory returned null for ActorID {actorId}.");

                actor.InitializeActor(actorId);
                actor.DeactivateActor();

                actors[actorId] = actor;
                states[actorId] = ActorSlotState.Free;
                freeActorIds.Add(actorId);
            }

            initialized = true;
        }

        public T Acquire(out ActorHandle handle)
        {
            EnsureInitialized();

            if (freeActorIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "No free actors available.");
            }

            return AcquireAt(freeActorIds.Min, out handle);
        }

        public T AcquireAt(int actorId, out ActorHandle handle)
        {
            EnsureInitialized();
            ValidateActorId(actorId);

            if (states[actorId] != ActorSlotState.Free)
            {
                throw new InvalidOperationException(
                    $"Actor {actorId} is not free.");
            }

            freeActorIds.Remove(actorId);

            generations[actorId]++;
            states[actorId] = ActorSlotState.Reserved;

            handle = new ActorHandle(
                PoolId,
                actorId,
                generations[actorId]);

            T actor = actors[actorId];
            actor.PrepareSpawn();

            return actor;
        }

        public void Activate(ActorHandle handle)
        {
            ValidateHandle(handle, ActorSlotState.Reserved);

            actors[handle.ActorId].ActivateActor();
            states[handle.ActorId] = ActorSlotState.Active;
        }

        public void Release(ActorHandle handle)
        {
            ValidateHandle(handle);

            actors[handle.ActorId].DeactivateActor();

            states[handle.ActorId] = ActorSlotState.Free;
            freeActorIds.Add(handle.ActorId);
        }

        IPhysicalActor IActorPool.Acquire(out ActorHandle handle)
        {
            return Acquire(out handle);
        }

        IPhysicalActor IActorPool.AcquireAt(int actorId, out ActorHandle handle)
        {
            return AcquireAt(actorId, out handle);
        }

        private void ValidateHandle(ActorHandle handle, ActorSlotState? expectedState = null)
        {
            if (handle.PoolId != PoolId)
                throw new InvalidOperationException("Wrong actor pool.");

            ValidateActorId(handle.ActorId);

            if (generations[handle.ActorId] != handle.Generation)
                throw new InvalidOperationException("Stale actor handle.");

            if (states[handle.ActorId] == ActorSlotState.Free)
                throw new InvalidOperationException("Actor is already free.");

            if (expectedState.HasValue &&
                states[handle.ActorId] != expectedState.Value)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedState.Value}, " +
                    $"but actor is {states[handle.ActorId]}.");
            }
        }

        private void ValidateActorId(int actorId)
        {
            if ((uint)actorId >= (uint)actors.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actorId));
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "Actor pool is not initialized.");
            }
        }
    }
}
