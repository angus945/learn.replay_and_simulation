using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Presentation;
using UnityEngine;

namespace SimulationCore.SimulationActor.Infrastructure
{
    public class ActorPool
    {
        public readonly Transform poolRoot;
        public readonly UnityEngine.Object prefab;
        public readonly List<IActor> actorPools = new List<IActor>();
        public readonly List<GameObject> actorGameObjects = new List<GameObject>();
        public readonly List<UnityActorBindingTag> actorBindingTags = new List<UnityActorBindingTag>();

        public ActorPool(Transform poolRoot, UnityEngine.Object prefab)
        {
            this.poolRoot = poolRoot;
            this.prefab = prefab;
        }
    }
    public class ActorPools
    {
        Transform poolsRoot;
        Dictionary<int, ActorPool> actorPools = new Dictionary<int, ActorPool>();

        internal ActorPools(Transform poolsRoot)
        {
            this.poolsRoot = poolsRoot;
        }
        internal void AddPool(int archetypeId, UnityEngine.Object prefab)
        {
            Transform poolRoot = new GameObject($"{archetypeId}_{prefab.name}").transform;
            poolRoot.SetParent(poolsRoot, false);

            ActorPool pool = new ActorPool(poolRoot, prefab);
            actorPools.Add(archetypeId, pool);
        }
        internal Transform GetParent(int archetypeId)
        {
            if (actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                return pool.poolRoot;
            }
            else
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
        }

        internal void Add<T>(int archetypeId, T actorComponent, GameObject instance, UnityActorBindingTag bindingTag) where T : IActor
        {
            if (!actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }

            pool.actorPools.Add(actorComponent);
            pool.actorGameObjects.Add(instance);
            pool.actorBindingTags.Add(bindingTag);
        }
        internal bool ContainArchetype(int archetypeId)
        {
            return actorPools.ContainsKey(archetypeId);
        }
        internal bool InPoolRange(int archetypeId, int slotId)
        {
            if (actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                return slotId >= 0 && slotId < pool.actorPools.Count;
            }
            else
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
        }
        internal void GetActor(int archetypeId, int slotId, out IActor actor, out GameObject gameObject, out UnityActorBindingTag bindingTag)
        {
            if (actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                if (slotId >= 0 && slotId < pool.actorPools.Count)
                {
                    actor = pool.actorPools[slotId] ?? throw new InvalidOperationException($"Actor at slot ID {slotId} in pool {archetypeId} is null.");
                    gameObject = pool.actorGameObjects[slotId] ?? throw new InvalidOperationException($"GameObject at slot ID {slotId} in pool {archetypeId} is null.");
                    bindingTag = pool.actorBindingTags[slotId] ?? throw new InvalidOperationException($"BindingTag at slot ID {slotId} in pool {archetypeId} is null.");
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot ID {slotId} is out of range for pool {archetypeId}.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
        }

        internal UnityEngine.Object GetPrefab(int archetypeId)
        {
            if (actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                return pool.prefab;
            }
            else
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
        }
        internal IReadOnlyList<GameObject> GetActorGameObjects(int archetypeId)
        {
            if (actorPools.TryGetValue(archetypeId, out ActorPool pool))
            {
                return pool.actorGameObjects.AsReadOnly();
            }
            else
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
        }
    }
}
