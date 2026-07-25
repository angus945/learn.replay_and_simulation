using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Application.Port
{
    public interface IActorBindingPort
    {
        void InstantiateActors<T>(int archetypeId, int capacity) where T : IActor;
        ActorHandle ActiveAndBindActor(EntityHandle entity, int archetypeId, int slotId);

        int ActiveActorCount { get; }
        ActorBinding GetActiveBinding(int index);

        bool HasBinding(EntityHandle entity);
        void Unbind(ActorBinding binding);

    }
}
