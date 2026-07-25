using System;
using System.Collections.Generic;
using SimulationCore.World.Contract;

namespace SimulationCore.World.Domain
{
    public class Systems
    {
        private readonly List<ISystem> systems = new();
        private readonly Dictionary<Type, int> typeToIndex = new();
        public int Count => systems.Count;

        internal void Add(ISystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (Contains(system))
            {
                throw new InvalidOperationException($"{system.GetType().Name} is already registered.");
            }

            systems.Add(system);
            typeToIndex[system.GetType()] = systems.Count - 1;
        }
        internal bool Contains(ISystem system)
        {
            return typeToIndex.ContainsKey(system.GetType());
        }
        public ISystem GetSystem(int index)
        {
            if (index < 0 || index >= systems.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return systems[index];
        }
    }
}