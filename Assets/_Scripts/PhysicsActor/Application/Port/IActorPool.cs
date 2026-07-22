using System;

public interface IActorPool
{
    int PoolId { get; }
    Type ActorType { get; }

    IPhysicalActor Acquire(out ActorHandle handle);
    IPhysicalActor AcquireAt(int actorId, out ActorHandle handle);

    void Activate(ActorHandle handle);
    void Release(ActorHandle handle);
}
