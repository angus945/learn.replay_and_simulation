namespace PhysicsActor.Application
{
    internal sealed class InitializeActorPoolsUseCase
    {
        private readonly ActorPoolState state;

        internal InitializeActorPoolsUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal void Execute()
        {
            int[] poolIds = state.GetSortedPoolIds();
            foreach (int poolId in poolIds)
            {
                state.GetPool(poolId).Initialize();
            }
        }
    }
}
