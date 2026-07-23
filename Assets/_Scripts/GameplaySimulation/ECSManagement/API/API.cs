using ECSManagement.Contract;
using ExternalIntent.API;

namespace ECSManagement.API
{
    public interface IEcsSystemRuntime
    {
        void HandleIntents(ulong tick, ICommittedIntentReader intents);
        void PrePhysicsTick(ulong tick, float deltaTime);
        void PostPhysicsTick(ulong tick, float deltaTime);
    }

    public interface IEcsWorld
    {
        IFilterBuilder CreateFilter();

        // void RegisterComponent<T>() where T : IComponent;
        // EntityHandle Spawn<TArguments>(IEntityRecipe<TArguments> recipe, in TArguments arguments);
        // void Destroy(EntityHandle entity);
        // void RegisterSystem(IEcsSystem system);

        // void AddComponent<T>(EntityHandle entity, T component) where T : IComponent;
        // bool HasComponent<T>(EntityHandle entity) where T : IComponent;
        // T GetComponent<T>(EntityHandle entity) where T : IComponent;
        bool TryGetComponent<T>(EntityHandle entity, out T component) where T : IComponent;
        void SetComponent<T>(EntityHandle entity, T component) where T : IComponent;
        // void RemoveComponent<T>(EntityHandle entity) where T : IComponent;
        // IEnumerable<ComponentEntry<T>> ReadComponents<T>() where T : IComponent;
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
