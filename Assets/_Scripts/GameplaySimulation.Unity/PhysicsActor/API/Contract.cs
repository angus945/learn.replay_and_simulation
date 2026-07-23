namespace PhysicsActor.Contract
{
    public interface IPhysicalActor
    {
        int ActorId { get; }

        void InitializeActor(int actorId);
        void PrepareSpawn();
        void ActivateActor();
        void DeactivateActor();
    }

    public readonly struct ActorHandle
    {
        public int PoolId { get; }
        public int ActorId { get; }
        public uint Generation { get; }

        public ActorHandle(int poolId, int actorId, uint generation)
        {
            PoolId = poolId;
            ActorId = actorId;
            Generation = generation;
        }
    }

    public readonly struct AcquireActorCommand
    {
        public readonly int PoolId;

        public AcquireActorCommand(int poolId)
        {
            PoolId = poolId;
        }
    }

    public readonly struct AcquireActorAtCommand
    {
        public readonly int PoolId;
        public readonly int ActorId;

        public AcquireActorAtCommand(int poolId, int actorId)
        {
            PoolId = poolId;
            ActorId = actorId;
        }
    }

    public readonly struct ActivatePendingActorsCommand
    {

    }

    public readonly struct ReleaseActorCommand
    {
        public readonly ActorHandle Handle;

        public ReleaseActorCommand(ActorHandle handle)
        {
            Handle = handle;
        }
    }
}
