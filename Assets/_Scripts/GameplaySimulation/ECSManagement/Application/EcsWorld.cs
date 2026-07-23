using System;
using System.Collections.Generic;
using ECSManagement.API;
using ECSManagement.Contract;
using ECSManagement.Domain;

namespace ECSManagement.Application
{
    public sealed class EcsWorld : IEcsWorld, IEcsSystemRuntime
    {
        private readonly List<ISystem> systems = new();
        private readonly List<EntityFilter> filters = new();
        private readonly EntityRegistry entities;
        private readonly ComponentStores components;
        private readonly EntityFactory entityFactory;

        public EcsWorld(int entityCapacity)
        {
            entities = new EntityRegistry(entityCapacity);
            components = new ComponentStores(entities);
            entityFactory = new EntityFactory(entities, components);
        }

        public void RegisterComponent<T>() where T : IComponent
        {
            components.RegisterStore<T>();
        }

        public IFilterBuilder CreateFilter()
        {
            return new EntityFilter(entities, components, RegisterBuiltFilter);
        }

        public EntityHandle Spawn<TArguments>(IEntityRecipe<TArguments> recipe, in TArguments arguments)
        {
            EntityHandle entity = entityFactory.Spawn(recipe, in arguments);
            RefreshFilters();
            return entity;
        }

        public void Destroy(EntityHandle entity)
        {
            entities.MarkDestroy(entity);
            components.RemoveAllComponents(entity);
            entities.CommitDestroy(entity);
            RefreshFilters();
        }

        public void RegisterSystem(ISystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (systems.Contains(system))
            {
                throw new InvalidOperationException(
                    $"{system.GetType().Name} is already registered.");
            }

            system.Initialize(this);
            systems.Add(system);
        }

        public void PrePhysicsTick(ulong tick, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                systems[i].PrePhysicsTick(tick, deltaTime);
            }
        }

        public void PostPhysicsTick(ulong tick, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                systems[i].PostPhysicsTick(tick, deltaTime);
            }
        }

        public void AddComponent<T>(EntityHandle entity, T component)
            where T : IComponent
        {
            components.AddComponent(entity, component);
            RefreshFilters();
        }

        public bool HasComponent<T>(EntityHandle entity) where T : IComponent
        {
            return components.Contains<T>(entity);
        }

        public T GetComponent<T>(EntityHandle entity) where T : IComponent
        {
            return components.Get<T>(entity);
        }

        public bool TryGetComponent<T>(EntityHandle entity, out T component)
            where T : IComponent
        {
            return components.TryGet(entity, out component);
        }

        public void SetComponent<T>(EntityHandle entity, T component)
            where T : IComponent
        {
            components.Set(entity, component);
        }

        public void RemoveComponent<T>(EntityHandle entity) where T : IComponent
        {
            components.Remove<T>(entity);
            RefreshFilters();
        }

        public IEnumerable<ComponentEntry<T>> ReadComponents<T>()
            where T : IComponent
        {
            return components.ReadAll<T>();
        }

        public bool TryGetEntityBySpawnSequence(
            ulong spawnSequence,
            out EntityHandle entity)
        {
            return entities.TryGetAliveEntityBySpawnSequence(
                spawnSequence,
                out entity);
        }

        public int EntityCountBySpawnSequence => entities.AliveEntityCount;

        public EntityHandle GetEntityBySpawnSequenceIndex(int index)
        {
            return entities.GetAliveEntityBySpawnSequenceIndex(index);
        }

        private void RegisterBuiltFilter(EntityFilter filter)
        {
            if (!filters.Contains(filter))
                filters.Add(filter);
        }

        private void RefreshFilters()
        {
            for (int i = 0; i < filters.Count; i++)
            {
                filters[i].Refresh();
            }
        }
    }
}
