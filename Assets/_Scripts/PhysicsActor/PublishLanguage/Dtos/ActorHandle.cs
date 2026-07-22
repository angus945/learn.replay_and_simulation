namespace PhysicsActor
{
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
}
