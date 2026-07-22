using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    internal sealed class AcquireActorUseCase
    {
        private readonly ActorPoolState state;

        internal AcquireActorUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal ActorHandle Execute(AcquireActorCommand command)
        {
            IActorPool pool = state.GetPool(command.PoolId);
            pool.Acquire(out ActorHandle handle);
            state.PendingActivations.Add(handle);

            return handle;
        }
    }
}
