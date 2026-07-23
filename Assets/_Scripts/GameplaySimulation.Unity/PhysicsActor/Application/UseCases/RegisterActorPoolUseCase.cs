using PhysicsActor.Application.Port;
using PhysicsActor.Contract;
using PhysicsActor.Domain;

namespace PhysicsActor.Application
{
    internal sealed class RegisterActorPoolUseCase
    {
        private readonly ActorPoolState state;

        internal RegisterActorPoolUseCase(ActorPoolState state)
        {
            this.state = state;
        }

        internal void Execute<T>(int poolId, int capacity, IActorFactory<T> factory) where T : class, IPhysicalActor
        {
            state.AddPool(new ActorPool<T>(poolId, factory, capacity));
        }
    }
}
