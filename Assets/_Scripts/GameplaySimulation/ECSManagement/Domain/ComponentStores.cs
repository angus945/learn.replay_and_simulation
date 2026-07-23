using System;
using System.Collections.Generic;
using ECSManagement.Contract;

namespace ECSManagement.Domain
{
    internal sealed class ComponentStores : IEntityBuildContext
    {
        private readonly Dictionary<Type, int> typeToIndex = new();
        private readonly List<IComponentStore> stores = new();
        private readonly EntityRegistry entityRegistry;

        public ComponentStores(EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry ??
                throw new ArgumentNullException(nameof(entityRegistry));
        }

        public void RegisterStore<T>() where T : IComponent
        {
            Type type = typeof(T);

            if (typeToIndex.ContainsKey(type))
            {
                throw new InvalidOperationException(
                    $"Component store for type {type.Name} is already registered.");
            }

            int index = stores.Count;
            typeToIndex[type] = index;

            stores.Add(new ComponentStore<T>(entityRegistry));
        }

        public void AddComponent<T>(EntityHandle entity, T component) where T : IComponent
        {
            GetStore<T>().Add(entity, component);
        }

        public bool Contains<T>(EntityHandle entity) where T : IComponent
        {
            return GetStore<T>().Contains(entity);
        }

        public bool Contains(EntityHandle entity, Type componentType)
        {
            if (componentType == null)
                throw new ArgumentNullException(nameof(componentType));

            if (!typeToIndex.TryGetValue(componentType, out int index))
            {
                throw new InvalidOperationException(
                    $"Component store for type {componentType.Name} is not registered.");
            }

            return stores[index].ContainsEntity(entity.Id);
        }

        public T Get<T>(EntityHandle entity) where T : IComponent
        {
            return GetStore<T>().Get(entity);
        }

        public bool TryGet<T>(EntityHandle entity, out T component) where T : IComponent
        {
            return GetStore<T>().TryGet(entity, out component);
        }

        public void Set<T>(EntityHandle entity, T component) where T : IComponent
        {
            GetStore<T>().Set(entity, component);
        }

        public void Remove<T>(EntityHandle entity) where T : IComponent
        {
            GetStore<T>().Remove(entity);
        }

        public IEnumerable<ComponentEntry<T>> ReadAll<T>() where T : IComponent
        {
            return GetStore<T>().ReadAllAlive();
        }

        internal void RemoveAllComponents(EntityHandle entity)
        {
            for (int i = 0; i < stores.Count; i++)
            {
                stores[i].RemoveEntity(entity.Id);
            }
        }

        private ComponentStore<T> GetStore<T>() where T : IComponent
        {
            Type type = typeof(T);
            if (!typeToIndex.TryGetValue(type, out int index))
            {
                throw new InvalidOperationException(
                    $"Component store for type {type.Name} is not registered.");
            }

            return (ComponentStore<T>)stores[index];
        }
    }
}
