using System;
using SimulationCore.World.Contract;

namespace SimulationCore.World.API
{
    public interface IEcsWorld
    {
        IFilterBuilder CreateFilter();
        void SpawnRequest<TArguments>(IEntityRecipe<TArguments> recipe, in TArguments arguments);
        void Destroy(EntityHandle entity);

        bool TryGetComponent<T>(EntityHandle entity, out T component) where T : IComponent;
        void SetComponent<T>(EntityHandle entity, T component) where T : IComponent;
    }
    public interface IFilterBuilder
    {
        IFilterBuilder With<T>() where T : IComponent;
        IFilterBuilder Without<T>() where T : IComponent;
        IEntityFilter Build();
    }

    public interface IEntityFilter
    {
        int EntityCount { get; }
        EntityHandle GetEntity(int index);
        bool Contains(EntityHandle entity);
    }
}
