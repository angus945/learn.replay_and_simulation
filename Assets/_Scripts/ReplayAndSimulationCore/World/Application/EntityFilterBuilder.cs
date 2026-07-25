using System;
using System.Collections.Generic;
using SimulationCore.World.API;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    public sealed class EntityFilterBuilder : IFilterBuilder
    {
        private readonly Entities entities;
        private readonly ComponentStores components;
        private readonly List<Type> requiredComponentTypes = new();
        private readonly List<Type> excludedComponentTypes = new();
        private readonly Action<EntityFilter> onFilterBuilded;

        public EntityFilterBuilder(Entities entities, ComponentStores components, Action<EntityFilter> onFilterBuilded)
        {
            this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
            this.components = components ?? throw new ArgumentNullException(nameof(components));
            this.onFilterBuilded = onFilterBuilded ?? throw new ArgumentNullException(nameof(onFilterBuilded));
        }
        public IFilterBuilder With<T>() where T : IComponent
        {
            AddUnique(requiredComponentTypes, typeof(T));
            return this;
        }
        public IFilterBuilder Without<T>() where T : IComponent
        {
            AddUnique(excludedComponentTypes, typeof(T));
            return this;
        }
        public IEntityFilter Build()
        {
            EntityFilter filter = new EntityFilter(entities, components, requiredComponentTypes, excludedComponentTypes);
            onFilterBuilded.Invoke(filter);
            return filter;
        }

        private static void AddUnique(List<Type> componentTypes, Type componentType)
        {
            if (!componentTypes.Contains(componentType))
                componentTypes.Add(componentType);
        }
    }
}