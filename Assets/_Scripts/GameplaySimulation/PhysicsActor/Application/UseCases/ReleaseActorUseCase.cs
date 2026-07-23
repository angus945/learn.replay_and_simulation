using PhysicsActor.Contract;
using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    internal sealed class ReleaseActorUseCase
    {
        private readonly ActorPoolState state;

        internal ReleaseActorUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal void Execute(ReleaseActorCommand command)
        {
            IActorPool pool = state.GetPool(command.Handle.PoolId);
            pool.Release(command.Handle);
        }
    }
}
