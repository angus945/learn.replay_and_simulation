using System;
using PhysicsActor;

namespace PhysicsActor.Domain
{
    public interface IActorPool
    {
        int PoolId { get; }
        Type ActorType { get; }

        void Initialize();
        IPhysicalActor Acquire(out ActorHandle handle);
        IPhysicalActor AcquireAt(int actorId, out ActorHandle handle);

        void Activate(ActorHandle handle);
        void Release(ActorHandle handle);
    }
}
