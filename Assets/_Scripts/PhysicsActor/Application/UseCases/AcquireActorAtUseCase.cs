using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    internal sealed class AcquireActorAtUseCase
    {
        private readonly ActorPoolState state;

        internal AcquireActorAtUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal ActorHandle Execute(AcquireActorAtCommand command)
        {
            IActorPool pool = state.GetPool(command.PoolId);
            pool.AcquireAt(command.ActorId, out ActorHandle handle);
            state.PendingActivations.Add(handle);

            return handle;
        }
    }
}
