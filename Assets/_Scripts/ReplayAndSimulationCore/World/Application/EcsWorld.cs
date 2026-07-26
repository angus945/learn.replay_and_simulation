using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    public sealed class SystemTicker
    {
        Dictionary<Type, (int, int)> systemToIndex;
        List<IPrePhysicsTick> prePhysicsTickSystems;
        List<IPostPhysicsTick> postPhysicsTickSystems;
        public int PrePhysicsTickSystemCount => prePhysicsTickSystems.Count;
        public int PostPhysicsTickSystemCount => postPhysicsTickSystems.Count;

        public SystemTicker()
        {
            systemToIndex = new Dictionary<Type, (int, int)>();
            prePhysicsTickSystems = new List<IPrePhysicsTick>();
            postPhysicsTickSystems = new List<IPostPhysicsTick>();
        }
        public void RegisterSystem(ISystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (systemToIndex.ContainsKey(system.GetType()))
            {
                throw new InvalidOperationException($"{system.GetType().Name} is already registered.");
            }

            int preIndex = -1;
            int postIndex = -1;

            if (system is IPrePhysicsTick)
            {
                preIndex = prePhysicsTickSystems.Count;
                prePhysicsTickSystems.Add((IPrePhysicsTick)system);
            }

            if (system is IPostPhysicsTick)
            {
                postIndex = postPhysicsTickSystems.Count;
                postPhysicsTickSystems.Add((IPostPhysicsTick)system);
            }

            systemToIndex[system.GetType()] = (preIndex, postIndex);
        }

        public void PrePhysicsTick(ulong tick, float deltaTime)
        {
            for (int i = 0; i < prePhysicsTickSystems.Count; i++)
            {
                prePhysicsTickSystems[i].PrePhysicsTick(tick, deltaTime);
            }
        }
        public void PostPhysicsTick(ulong tick, float deltaTime)
        {
            for (int i = 0; i < postPhysicsTickSystems.Count; i++)
            {
                postPhysicsTickSystems[i].PostPhysicsTick(tick, deltaTime);
            }
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
        SystemTicker systemTicker;

        public EcsWorld(int entityCapacity, ICommandHandleRegistryPort registryPort)
        {
            this.registryPort = registryPort ?? throw new ArgumentNullException(nameof(registryPort));

            entities = new Entities(entityCapacity);
            components = new ComponentStores();
            systems = new Systems();

            filters = new EntityFilters(entities, components);
            spawner = new EntitySpawner(new EntityFactory(entities, components));
            destroyer = new EntityDestroyer(entities, components);

            systemTicker = new SystemTicker();
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
            systemTicker.RegisterSystem(system);
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
            systemTicker.PrePhysicsTick(tick, delta);
        }

        void ISimulationWorld.PostPhysicsTick(ulong tick, float delta)
        {
            systemTicker.PostPhysicsTick(tick, delta);
        }
        void ISimulationWorld.CommitStructuralChanges()
        {
            destroyer.CommitDestroyRequests();
            spawner.CommitSpawnRequests();
            filters.RefreshFilters();
        }
    }
}
