using System;
using System.Collections.Generic;
using ECSManagement.Contract;

namespace ECSManagement.Domain
{
    internal interface IComponentStore
    {
        void RemoveEntity(int entityId);
        bool ContainsEntity(int entityId);
    }

    internal sealed class ComponentStore<T> : IComponentStore where T : IComponent
    {
        private readonly EntityRegistry entities;
        private readonly SortedDictionary<int, T> components = new();

        public ComponentStore(EntityRegistry entities)
        {
            this.entities = entities ??
                throw new ArgumentNullException(nameof(entities));
        }

        public void Add(EntityHandle entity, T component)
        {
            ValidateWritable(entity);

            if (components.ContainsKey(entity.Id))
            {
                throw new InvalidOperationException(
                    $"Entity {entity.Id} already has component {typeof(T).Name}.");
            }

            components.Add(entity.Id, component);
        }

        public bool Contains(EntityHandle entity)
        {
            return entities.CanBuild(entity) &&
                   components.ContainsKey(entity.Id);
        }

        public T Get(EntityHandle entity)
        {
            ValidateWritable(entity);

            if (!components.TryGetValue(entity.Id, out T component))
            {
                throw new InvalidOperationException(
                    $"Entity {entity.Id} does not have component {typeof(T).Name}.");
            }

            return component;
        }

        public bool TryGet(EntityHandle entity, out T component)
        {
            component = default;

            if (!entities.CanBuild(entity))
                return false;

            return components.TryGetValue(entity.Id, out component);
        }

        public void Set(EntityHandle entity, T component)
        {
            ValidateWritable(entity);

            if (!components.ContainsKey(entity.Id))
            {
                throw new InvalidOperationException(
                    $"Entity {entity.Id} does not have component {typeof(T).Name}.");
            }

            components[entity.Id] = component;
        }

        public void Remove(EntityHandle entity)
        {
            ValidateWritable(entity);
            components.Remove(entity.Id);
        }

        public IEnumerable<ComponentEntry<T>> ReadAllAlive()
        {
            foreach (KeyValuePair<int, T> pair in components)
            {
                if (entities.TryGetAliveEntity(pair.Key, out EntityHandle entity))
                    yield return new ComponentEntry<T>(entity, pair.Value);
            }
        }

        void IComponentStore.RemoveEntity(int entityId)
        {
            components.Remove(entityId);
        }

        bool IComponentStore.ContainsEntity(int entityId)
        {
            return components.ContainsKey(entityId);
        }

        private void ValidateWritable(EntityHandle entity)
        {
            if (!entities.CanBuild(entity))
            {
                throw new InvalidOperationException(
                    "Entity is stale or unavailable.");
            }
        }
    }
}
