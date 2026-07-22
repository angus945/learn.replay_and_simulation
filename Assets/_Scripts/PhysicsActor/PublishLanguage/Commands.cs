namespace PhysicsActor
{
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
