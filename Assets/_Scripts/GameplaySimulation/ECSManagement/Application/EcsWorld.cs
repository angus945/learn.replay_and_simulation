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
        private readonly List<ISpawnRequest> pendingSpawnRequests = new();
        private readonly List<EntityHandle> pendingDestroyRequests = new();
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

        public void SpawnRequest<TArguments>(
            IEntityRecipe<TArguments> recipe,
            in TArguments arguments)
        {
            pendingSpawnRequests.Add(
                new PendingSpawnRequest<TArguments>(recipe, in arguments));
        }

        public void Destroy(EntityHandle entity)
        {
            entities.MarkDestroy(entity);
            pendingDestroyRequests.Add(entity);
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

        public void CommitStructuralChanges()
        {
            if (pendingDestroyRequests.Count == 0 &&
                pendingSpawnRequests.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pendingDestroyRequests.Count; i++)
            {
                EntityHandle entity = pendingDestroyRequests[i];
                components.RemoveAllComponents(entity);
                entities.CommitDestroy(entity);
            }

            pendingDestroyRequests.Clear();

            for (int i = 0; i < pendingSpawnRequests.Count; i++)
            {
                pendingSpawnRequests[i].Commit(entityFactory);
            }

            pendingSpawnRequests.Clear();
            RefreshFilters();
        }

        public void AddComponent<T>(EntityHandle entity, T component)
            where T : IComponent
        {
            EnsureAlive(entity);
            components.AddComponent(entity, component);
            RefreshFilters();
        }

        public bool HasComponent<T>(EntityHandle entity) where T : IComponent
        {
            if (!entities.IsAlive(entity))
                return false;

            return components.Contains<T>(entity);
        }

        public T GetComponent<T>(EntityHandle entity) where T : IComponent
        {
            EnsureAlive(entity);
            return components.Get<T>(entity);
        }

        public bool TryGetComponent<T>(EntityHandle entity, out T component)
            where T : IComponent
        {
            if (!entities.IsAlive(entity))
            {
                component = default;
                return false;
            }

            return components.TryGet(entity, out component);
        }

        public void SetComponent<T>(EntityHandle entity, T component)
            where T : IComponent
        {
            EnsureAlive(entity);
            components.Set(entity, component);
        }

        public void RemoveComponent<T>(EntityHandle entity) where T : IComponent
        {
            EnsureAlive(entity);
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

        private void EnsureAlive(EntityHandle entity)
        {
            if (!entities.IsAlive(entity))
                throw new InvalidOperationException("Entity is not alive.");
        }

        private interface ISpawnRequest
        {
            EntityHandle Commit(EntityFactory factory);
        }

        private sealed class PendingSpawnRequest<TArguments> : ISpawnRequest
        {
            private readonly IEntityRecipe<TArguments> recipe;
            private readonly TArguments arguments;

            public PendingSpawnRequest(
                IEntityRecipe<TArguments> recipe,
                in TArguments arguments)
            {
                this.recipe = recipe ??
                    throw new ArgumentNullException(nameof(recipe));
                this.arguments = arguments;
            }

            public EntityHandle Commit(EntityFactory factory)
            {
                return factory.Spawn(recipe, in arguments);
            }
        }
    }
}
