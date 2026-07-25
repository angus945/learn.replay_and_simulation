using System;
using System.Collections.Generic;
using SimulationCore.World.API;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    public class EntityFilters
    {
        Entities entities;
        ComponentStores components;

        public int Count => filters.Count;
        private readonly List<EntityFilter> filters = new();

        public EntityFilters(Entities entities, ComponentStores components)
        {
            this.entities = entities;
            this.components = components;
        }
        public IFilterBuilder CreateFilter()
        {
            return new EntityFilterBuilder(entities, components, RegisterBuiltFilter);
        }
        private void RegisterBuiltFilter(EntityFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            if (filters.Contains(filter))
            {
                throw new InvalidOperationException("Filter is already registered.");
            }

            filters.Add(filter);
        }

        internal void RefreshFilters()
        {
            for (int i = 0; i < filters.Count; i++)
            {
                filters[i].RebuildMatches();
            }
        }
    }
    public sealed class EntityFilter : IEntityFilter
    {
        private readonly Entities entities;
        private readonly ComponentStores components;
        private readonly IReadOnlyList<Type> requiredComponentTypes;
        private readonly IReadOnlyList<Type> excludedComponentTypes;
        private readonly List<Entity> matchingEntities;

        public EntityFilter(Entities entities, ComponentStores components, IReadOnlyList<Type> requiredComponentTypes, IReadOnlyList<Type> excludedComponentTypes)
        {
            this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
            this.components = components ?? throw new ArgumentNullException(nameof(components));

            this.requiredComponentTypes = requiredComponentTypes;
            this.excludedComponentTypes = excludedComponentTypes;

            matchingEntities = new List<Entity>();
        }

        public int EntityCount => matchingEntities.Count;
        public bool Contains(EntityHandle entityHandle)
        {
            if (!entities.IsAlive(entityHandle.SlotId, entityHandle.SequenceId))
                return false;

            Entity entity = entities.GetEntity(entityHandle.SlotId, entityHandle.SequenceId);
            return Matches(entity);
        }

        public EntityHandle GetEntity(int index)
        {
            if ((uint)index >= (uint)matchingEntities.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            Entity entity = matchingEntities[index];
            return new EntityHandle(entity.SlotId, entity.SequenceId);
        }

        public void RebuildMatches()
        {
            matchingEntities.Clear();

            for (int i = 0; i < entities.AliveEntityCount; i++)
            {
                Entity entity = entities.GetAliveEntityBySpawnSequence(i);
                if (Matches(entity))
                    matchingEntities.Add(entity);
            }
        }
        bool Matches(Entity entity)
        {
            for (int i = 0; i < requiredComponentTypes.Count; i++)
            {
                Type requiredType = requiredComponentTypes[i];
                if (!components.Contains(entity.SlotId, requiredType))
                {
                    return false;
                }
            }

            for (int i = 0; i < excludedComponentTypes.Count; i++)
            {
                Type excludedType = excludedComponentTypes[i];
                if (components.Contains(entity.SlotId, excludedType))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
