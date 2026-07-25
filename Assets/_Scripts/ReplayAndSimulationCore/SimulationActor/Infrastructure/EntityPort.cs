using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.API;
using SimulationCore.World.Contract;
using System;
using SimulationCore.SimulationActor.Application;

namespace SimulationCore.SimulationActor.Infrastructure
{
    public class EntityPort : IEntityPort
    {
        IEcsWorld world;
        IEntityFilter actorFilter;

        public EntityPort(IEcsWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            actorFilter = world.CreateFilter()
                .With<ActorArchetypeComponent>()
                .With<ActorTransformState>()
                .Build();
        }

        public int EntityCount => actorFilter.EntityCount;
        public ActorArchetypeComponent GetActorArchetypeComponent(EntityHandle entity)
        {
            if (!actorFilter.Contains(entity))
                throw new InvalidOperationException($"Entity {entity} does not have an ActorArchetypeComponent.");

            if (!world.TryGetComponent<ActorArchetypeComponent>(entity, out var component))
                throw new InvalidOperationException($"Failed to retrieve ActorArchetypeComponent for entity {entity}.");

            return component;
        }
        public EntityHandle GetEntity(int index)
        {
            if (index < 0 || index >= actorFilter.EntityCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Valid range is 0 to {actorFilter.EntityCount - 1}.");

            return actorFilter.GetEntity(index);
        }
    }
}
