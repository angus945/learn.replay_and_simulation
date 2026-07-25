using System;
using System.Collections.Generic;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    class BuildContext : IEntityBuildContext
    {
        List<IComponent> components = new();
        Dictionary<int, Type> componentTypes = new();
        HashSet<Type> componentTypeSet = new();

        public int ComponentCount => components.Count;

        public void AddComponent<T>(T component) where T : IComponent
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            Type componentType = typeof(T);
            if (componentTypeSet.Contains(componentType))
            {
                throw new InvalidOperationException(
                    $"Component of type {componentType.Name} has already been added to the build context.");
            }

            components.Add(component);
            componentTypes[components.Count - 1] = componentType;
            componentTypeSet.Add(componentType);
        }
        public IComponent GetComponent(int index)
        {
            if (index < 0 || index >= components.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            return components[index];
        }
        public void Clear()
        {
            components.Clear();
            componentTypes.Clear();
            componentTypeSet.Clear();
        }
    }
    internal sealed class EntityFactory
    {
        private readonly Entities entities;
        private readonly ComponentStores componentStores;
        BuildContext buildContext = new BuildContext();

        public EntityFactory(Entities entities, ComponentStores componentStores)
        {
            this.entities = entities ??
                throw new ArgumentNullException(nameof(entities));
            this.componentStores = componentStores ??
                throw new ArgumentNullException(nameof(componentStores));
        }

        public EntityHandle Spawn<TArguments>(IEntityRecipe<TArguments> recipe, in TArguments arguments)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            buildContext.Clear();

            try
            {
                recipe.Build(buildContext, in arguments);
                Entity entity = entities.Create();
                for (int i = 0; i < buildContext.ComponentCount; i++)
                {
                    IComponent component = buildContext.GetComponent(i);
                    Type componentType = component.GetType();
                    componentStores.AddComponent(entity.SlotId, componentType, component);
                }

                return new EntityHandle(entity.SlotId, entity.SequenceId);
            }
            catch
            {
                throw;
            }
        }
    }
}
