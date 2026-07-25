using System;
using System.Collections.Generic;
using SimulationCore.World.Contract;

namespace SimulationCore.World.Domain
{
    internal interface IComponentStore
    {
        void AddComponent(int slotId, IComponent component);
        bool Contains(int slotId);
        void RemoveComponent(int slotId);
    }
    internal sealed class ComponentStore<T> : IComponentStore where T : IComponent
    {
        private readonly SortedDictionary<int, T> components = new();

        public void AddComponent(int slotId, IComponent component)
        {
            if (component is not T typedComponent)
                throw new ArgumentException($"Component must be of type {typeof(T).Name}.", nameof(component));

            components[slotId] = typedComponent;
        }
        public void SetComponent(int slotId, T component)
        {
            components[slotId] = component;
        }
        public bool Contains(int slotId)
        {
            return components.ContainsKey(slotId);
        }
        public void RemoveComponent(int slotId)
        {
            components.Remove(slotId);
        }
        public T GetComponent(int slotId)
        {
            if (!components.TryGetValue(slotId, out T component))
                throw new InvalidOperationException($"Component of type {typeof(T).Name} not found for entity with SlotId {slotId}.");

            return component;
        }
    }
    public class ComponentStores
    {
        private readonly List<IComponentStore> stores = new();
        private readonly Dictionary<Type, int> typeToIndex = new();

        public void RegisterStore<T>() where T : IComponent
        {
            Type type = typeof(T);

            if (typeToIndex.ContainsKey(type))
            {
                throw new InvalidOperationException(
                    $"Component store for type {type.Name} is already registered.");
            }

            typeToIndex[type] = stores.Count;
            stores.Add(new ComponentStore<T>());
        }
        public void AddComponent<T>(int slotId, T component) where T : IComponent
        {
            GetStore<T>().AddComponent(slotId, component);
        }
        public void AddComponent(int slotId, Type componentType, IComponent component)
        {
            if (!typeToIndex.TryGetValue(componentType, out int index))
            {
                throw new InvalidOperationException(
                    $"Component store for type {componentType.Name} is not registered.");
            }

            stores[index].AddComponent(slotId, component);
        }
        public bool TryGetComponent<T>(int slotId, out T component) where T : IComponent
        {
            ComponentStore<T> store = GetStore<T>();

            if (!store.Contains(slotId))
            {
                component = default;
                return false;
            }

            component = store.GetComponent(slotId);
            return true;
        }
        internal void SetComponent<T>(int slotId, T component) where T : IComponent
        {
            GetStore<T>().SetComponent(slotId, component);
        }
        internal void RemoveAllComponents(int slotId)
        {
            foreach (var store in stores)
            {
                if (store.Contains(slotId))
                {
                    store.RemoveComponent(slotId);
                }
            }
        }
        public bool Contains(int slotId, Type componentType)
        {
            if (!typeToIndex.TryGetValue(componentType, out int index))
            {
                throw new InvalidOperationException(
                    $"Component store for type {componentType.Name} is not registered.");
            }

            return stores[index].Contains(slotId);
        }

        ComponentStore<T> GetStore<T>() where T : IComponent
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