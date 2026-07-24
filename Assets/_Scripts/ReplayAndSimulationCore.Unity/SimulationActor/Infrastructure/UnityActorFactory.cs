using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using UnityEngine;

namespace SimulationCore.SimulationActor.Infrastructure
{
    public sealed class UnityActorInstancePort : IActorInstancePort
    {
        Dictionary<int, UnityEngine.Object> poolPrefabs = new Dictionary<int, UnityEngine.Object>();

        public void RegisterPrefab<T>(int poolId, T prefab) where T : MonoBehaviour, IActor
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            if (poolPrefabs.ContainsKey(poolId))
                throw new InvalidOperationException($"Prefab for pool {poolId} is already registered.");

            poolPrefabs.Add(poolId, prefab);
        }
        public T[] CreateActorInstances<T>(int poolId, int capacity) where T : IActor
        {
            if (!poolPrefabs.TryGetValue(poolId, out UnityEngine.Object prefab))
            {
                throw new InvalidOperationException($"Prefab for pool {poolId} is not registered.");
            }

            T[] actorInstances = new T[capacity];

            for (int i = 0; i < capacity; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab) as GameObject;
                actorInstances[i] = instance.GetComponent<T>();
            }

            return actorInstances;
        }
    }
}
