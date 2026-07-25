using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    sealed class EntitySpawner
    {
        private EntityFactory entityFactory;

        private readonly List<ISpawnRequest> pendingSpawnRequests = new();

        public EntitySpawner(EntityFactory entityFactory)
        {
            this.entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        }
        internal void SpawnRequest<TArgs>(IEntityRecipe<TArgs> spawnPlayerRecipe, TArgs spawnPlayerArguments) where TArgs : IEntityArguments
        {
            pendingSpawnRequests.Add(new PendingSpawnRequest<TArgs>(spawnPlayerRecipe, spawnPlayerArguments));
        }
        public void CommitSpawnRequests()
        {
            for (int i = 0; i < pendingSpawnRequests.Count; i++)
            {
                pendingSpawnRequests[i].Commit(entityFactory);
            }
            pendingSpawnRequests.Clear();
        }

        private interface ISpawnRequest
        {
            EntityHandle Commit(EntityFactory factory);
        }
        private sealed class PendingSpawnRequest<TArguments> : ISpawnRequest
        {
            private readonly IEntityRecipe<TArguments> recipe;
            private readonly TArguments arguments;

            public PendingSpawnRequest(IEntityRecipe<TArguments> recipe, in TArguments arguments)
            {
                this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
                this.arguments = arguments;
            }

            public EntityHandle Commit(EntityFactory factory)
            {
                return factory.Spawn(recipe, in arguments);
            }
        }
    }
    sealed class EntityDestroyer
    {
        Entities entities;
        ComponentStores components;

        readonly List<EntityHandle> pendingDestroyRequests = new();

        public EntityDestroyer(Entities entities, ComponentStores components)
        {
            this.entities = entities;
            this.components = components;
        }

        public void DestroyRequest(EntityHandle entity)
        {
            pendingDestroyRequests.Add(entity);
            entities.MarkForDestroy(entity);
        }
        public void CommitDestroyRequests()
        {
            for (int i = 0; i < pendingDestroyRequests.Count; i++)
            {
                EntityHandle entity = pendingDestroyRequests[i];
                components.RemoveAllComponents(entity.SlotId);
                entities.CommitDestroy(entity);
            }
            pendingDestroyRequests.Clear();
        }
    }

    public sealed class EcsWorld : IEcsWorld, ISimulationWorld
    {
        ICommandHandleRegistryPort registryPort;

        Entities entities;
        ComponentStores components;
        Systems systems;

        EntityFilters filters;
        EntitySpawner spawner;
        EntityDestroyer destroyer;

        public EcsWorld(int entityCapacity, ICommandHandleRegistryPort registryPort)
        {
            this.registryPort = registryPort ?? throw new ArgumentNullException(nameof(registryPort));

            entities = new Entities(entityCapacity);
            components = new ComponentStores();
            systems = new Systems();
            filters = new EntityFilters(entities, components);
            spawner = new EntitySpawner(new EntityFactory(entities, components));
            destroyer = new EntityDestroyer(entities, components);
        }

        public void RegisterComponent<TComponent>() where TComponent : IComponent
        {
            components.RegisterStore<TComponent>();
        }
        public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (systems.Contains(system))
            {
                throw new InvalidOperationException($"{system.GetType().Name} is already registered.");
            }

            systems.Add(system);
        }
        public void InitializeSystems()
        {
            for (int i = 0; i < systems.Count; i++)
            {
                systems.GetSystem(i).Initialize(this, registryPort);
            }
        }

        IFilterBuilder IEcsWorld.CreateFilter()
        {
            return filters.CreateFilter();
        }

        void IEcsWorld.SpawnRequest<TArgs>(IEntityRecipe<TArgs> recipe, TArgs arguments)
        {
            spawner.SpawnRequest(recipe, arguments);
        }

        void IEcsWorld.DestroyRequest(EntityHandle entity)
        {
            destroyer.DestroyRequest(entity);
        }

        bool IEcsWorld.TryGetComponent<T>(EntityHandle entity, out T component)
        {
            if (!entities.IsAlive(entity.SlotId, entity.SequenceId))
            {
                component = default;
                return false;
            }

            return components.TryGetComponent(entity.SlotId, out component);
        }
        void IEcsWorld.SetComponent<T>(EntityHandle entity, T component)
        {
            if (!entities.IsAlive(entity.SlotId, entity.SequenceId))
            {
                throw new InvalidOperationException($"Entity {entity} is not alive.");
            }

            components.SetComponent(entity.SlotId, component);
        }


        void ISimulationWorld.PrePhysicsTick(ulong tick, float delta)
        {
            // throw new NotImplementedException();
        }

        void ISimulationWorld.PostPhysicsTick(ulong tick, float delta)
        {
            // throw new NotImplementedException();
        }
        void ISimulationWorld.CommitStructuralChanges()
        {
            destroyer.CommitDestroyRequests();
            spawner.CommitSpawnRequests();
            filters.RefreshFilters();
        }
    }
}
