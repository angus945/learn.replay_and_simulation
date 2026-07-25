using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Application.Port
{
    public interface IActorBindingPort
    {
        void CreateActorInstances<T>(int archetypeId, int capacity) where T : IActor;
        ActorHandle ActiveAndBindActor(EntityHandle entity, int archetypeId, int slotId);

        int ActiveActorCount { get; }
        ActorBinding GetBinding(int slotId);

        bool HasBinding(EntityHandle entity);
        void Unbind(ActorBinding binding);

    }
}
