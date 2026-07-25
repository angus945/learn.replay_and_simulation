using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Application
{
    public interface IEntityPort
    {
        int EntityCount { get; }
        EntityHandle GetEntity(int index);

        ActorArchetypeComponent GetActorArchetypeComponent(EntityHandle entity);
    }
}
