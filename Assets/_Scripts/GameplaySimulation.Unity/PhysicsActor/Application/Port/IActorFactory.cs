using PhysicsActor;
using PhysicsActor.Contract;

namespace PhysicsActor.Application.Port
{
    public interface IActorFactory<out T> where T : class, IPhysicalActor
    {
        T CreateActor();
    }
}
