using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.Contract;
using UnityEngine;

namespace SimulationCore.SimulationActor.Infrastructure
{
    public sealed class UnityActorInstancePort : IActorBindingPort
    {
        Transform poolsRoot;

        Dictionary<int, UnityEngine.Object> poolPrefabs = new Dictionary<int, UnityEngine.Object>();
        Dictionary<int, Transform> poolRoots = new Dictionary<int, Transform>();
        Dictionary<int, List<IActor>> actorPools = new Dictionary<int, List<IActor>>();
        Dictionary<int, List<GameObject>> actorGameObjects = new Dictionary<int, List<GameObject>>();
        SortedList<int, ActorBinding> sortedBindings = new SortedList<int, ActorBinding>();
        Dictionary<EntityHandle, ActorBinding> entityBindings = new Dictionary<EntityHandle, ActorBinding>();
        public int ActiveActorCount => sortedBindings.Count;

        public UnityActorInstancePort(Transform poolsRoot)
        {
            this.poolsRoot = poolsRoot;
        }
        public void RegisterPrefab<T>(int archetypeId, T prefab) where T : MonoBehaviour, IActor
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            if (poolPrefabs.ContainsKey(archetypeId))
                throw new InvalidOperationException($"Prefab for pool {archetypeId} is already registered.");

            poolPrefabs.Add(archetypeId, prefab);
            actorPools[archetypeId] = new List<IActor>();
            actorGameObjects[archetypeId] = new List<GameObject>();
            poolRoots[archetypeId] = new GameObject($"ActorPool_{archetypeId}").transform;
            poolRoots[archetypeId].SetParent(poolsRoot, false);
        }
        public void CreateActorInstances<T>(int archetypeId, int capacity) where T : IActor
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            if (!poolPrefabs.TryGetValue(archetypeId, out UnityEngine.Object prefab))
            {
                throw new InvalidOperationException($"Prefab for pool {archetypeId} is not registered.");
            }

            actorPools[archetypeId] = new List<IActor>(capacity);
            actorGameObjects[archetypeId] = new List<GameObject>(capacity);
            Transform poolRoot = poolRoots[archetypeId];

            for (int i = 0; i < capacity; i++)
            {
                UnityEngine.Object clonedObject = UnityEngine.Object.Instantiate(prefab, poolRoot);
                GameObject instance = null;

                if (clonedObject is GameObject clonedGameObject)
                {
                    instance = clonedGameObject;
                }
                else if (clonedObject is Component clonedComponent)
                {
                    instance = clonedComponent.gameObject;
                }

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab for pool {archetypeId} must instantiate as a GameObject or Component.");
                }

                T actorComponent = instance.GetComponent<T>();
                if (actorComponent == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    throw new InvalidOperationException(
                        $"Prefab for pool {archetypeId} does not contain an actor component of type {typeof(T).Name}.");
                }

                instance.SetActive(false);
                actorPools[archetypeId].Add(actorComponent);
                actorGameObjects[archetypeId].Add(instance);
            }
        }

        public ActorHandle ActiveAndBindActor(EntityHandle entity, int archetypeId, int slotId)
        {
            if (!actorPools.TryGetValue(archetypeId, out List<IActor> pool))
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }
            if (!actorGameObjects.TryGetValue(archetypeId, out List<GameObject> gameObjects))
            {
                throw new InvalidOperationException($"Actor GameObject list for archetype {archetypeId} does not exist.");
            }

            if (slotId < 0 || slotId >= pool.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot ID {slotId} is out of range for pool {archetypeId}.");
            }

            IActor actor = pool[slotId];
            GameObject actorGameObject = gameObjects[slotId];
            if (actor == null)
            {
                throw new InvalidOperationException($"No actor instance found at slot {slotId} in pool {archetypeId}.");
            }
            if (actorGameObject == null)
            {
                throw new InvalidOperationException($"No GameObject instance found at slot {slotId} in pool {archetypeId}.");
            }

            // Activate the actor instance
            actorGameObject.SetActive(true);

            // Create a binding and store it
            ActorHandle handle = new ActorHandle(archetypeId, slotId);
            ActorBinding binding = new ActorBinding(entity, handle);
            entityBindings[entity] = binding;
            sortedBindings[slotId] = binding;

            return handle;
        }

        public ActorBinding GetBinding(int slotId)
        {
            if (sortedBindings.TryGetValue(slotId, out ActorBinding binding))
            {
                return binding;
            }
            else
            {
                throw new InvalidOperationException($"No binding found for slot ID {slotId}.");
            }
        }

        public bool HasBinding(EntityHandle entity)
        {
            return entityBindings.ContainsKey(entity);
        }

        public void Unbind(ActorBinding binding)
        {
            if (!entityBindings.TryGetValue(binding.Entity, out ActorBinding existingBinding) || existingBinding != binding)
            {
                throw new InvalidOperationException($"The provided binding does not match the existing binding for entity {binding.Entity}.");
            }

            // Deactivate the actor instance
            if (actorGameObjects.TryGetValue(binding.Actor.ArchetypeId, out List<GameObject> gameObjects))
            {
                int slotId = binding.Actor.SlotId;
                if (slotId >= 0 && slotId < gameObjects.Count)
                {
                    GameObject actorGameObject = gameObjects[slotId];
                    if (actorGameObject != null)
                    {
                        actorGameObject.SetActive(false);
                    }
                }
            }

            // Remove the binding from both dictionaries
            entityBindings.Remove(binding.Entity);
            sortedBindings.Remove(binding.Actor.SlotId);
        }


    }
}
