using System;
using System.Collections.Generic;
using ECSManagement.API;
using ECSManagement.Contract;
using ECSManagement.Domain;

namespace ECSManagement.Application
{
    public sealed class EntityFilter : IFilterBuilder, IEntityFilter
    {
        private readonly EntityRegistry entities;
        private readonly ComponentStores components;
        private readonly Action<EntityFilter> registerBuiltFilter;
        private readonly List<Type> requiredComponentTypes = new();
        private readonly List<Type> excludedComponentTypes = new();
        private readonly List<EntityHandle> matchingEntities;

        private bool isBuilt;

        internal EntityFilter(
            EntityRegistry entities,
            ComponentStores components,
            Action<EntityFilter> registerBuiltFilter)
        {
            this.entities = entities ??
                throw new ArgumentNullException(nameof(entities));
            this.components = components ??
                throw new ArgumentNullException(nameof(components));
            this.registerBuiltFilter = registerBuiltFilter ??
                throw new ArgumentNullException(nameof(registerBuiltFilter));
            matchingEntities = new List<EntityHandle>(this.entities.Capacity);
        }

        public int EntityCount
        {
            get
            {
                EnsureBuilt();
                return matchingEntities.Count;
            }
        }

        public IFilterBuilder With<T>() where T : IComponent
        {
            EnsureNotBuilt();
            AddUnique(requiredComponentTypes, typeof(T));
            return this;
        }

        public IFilterBuilder Without<T>() where T : IComponent
        {
            EnsureNotBuilt();
            AddUnique(excludedComponentTypes, typeof(T));
            return this;
        }

        public IEntityFilter Build()
        {
            EnsureNotBuilt();
            RebuildMatches();
            isBuilt = true;
            registerBuiltFilter(this);
            return this;
        }

        public EntityHandle GetEntity(int index)
        {
            EnsureBuilt();

            if ((uint)index >= (uint)matchingEntities.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return matchingEntities[index];
        }

        public bool Contains(EntityHandle entity)
        {
            EnsureBuilt();

            for (int i = 0; i < matchingEntities.Count; i++)
            {
                if (matchingEntities[i] == entity)
                    return true;
            }

            return false;
        }

        internal void Refresh()
        {
            EnsureBuilt();
            RebuildMatches();
        }

        private void RebuildMatches()
        {
            matchingEntities.Clear();

            for (int i = 0; i < entities.AliveEntityCount; i++)
            {
                EntityHandle entity = entities.GetAliveEntityBySpawnSequenceIndex(i);
                if (Matches(entity))
                    matchingEntities.Add(entity);
            }
        }

        private bool Matches(EntityHandle entity)
        {
            for (int i = 0; i < requiredComponentTypes.Count; i++)
            {
                if (!components.Contains(entity, requiredComponentTypes[i]))
                    return false;
            }

            for (int i = 0; i < excludedComponentTypes.Count; i++)
            {
                if (components.Contains(entity, excludedComponentTypes[i]))
                    return false;
            }

            return true;
        }

        private void EnsureBuilt()
        {
            if (!isBuilt)
                throw new InvalidOperationException("Entity filter has not been built.");
        }

        private void EnsureNotBuilt()
        {
            if (isBuilt)
                throw new InvalidOperationException("Entity filter has already been built.");
        }

        private static void AddUnique(List<Type> componentTypes, Type componentType)
        {
            if (!componentTypes.Contains(componentType))
                componentTypes.Add(componentType);
        }
    }
}
