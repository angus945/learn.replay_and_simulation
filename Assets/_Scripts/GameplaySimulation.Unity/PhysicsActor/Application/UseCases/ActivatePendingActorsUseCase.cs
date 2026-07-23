using PhysicsActor.Contract;
using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    internal sealed class ActivatePendingActorsUseCase
    {
        private readonly ActorPoolState state;

        internal ActivatePendingActorsUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal void Execute(ActivatePendingActorsCommand command)
        {
            state.PendingActivations.Sort(CompareActorHandle);

            foreach (ActorHandle handle in state.PendingActivations)
            {
                IActorPool pool = state.GetPool(handle.PoolId);
                pool.Activate(handle);
            }

            state.PendingActivations.Clear();
        }

        private static int CompareActorHandle(ActorHandle a, ActorHandle b)
        {
            int poolCompare = a.PoolId.CompareTo(b.PoolId);
            if (poolCompare != 0)
                return poolCompare;

            return a.ActorId.CompareTo(b.ActorId);
        }
    }
}
