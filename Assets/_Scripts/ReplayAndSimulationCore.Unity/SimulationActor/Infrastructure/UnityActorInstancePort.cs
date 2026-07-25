using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Presentation;
using SimulationCore.World.Contract;
using UnityEngine;

namespace SimulationCore.SimulationActor.Infrastructure
{

    public sealed class UnityActorInstancePort : IActorBindingPort
    {
        ActorPools actorPools;
        ActorBindings actorBindings;

        public int ActiveActorCount => actorBindings.Count;

        public UnityActorInstancePort(Transform poolsRoot)
        {
            actorPools = new ActorPools(poolsRoot);
            actorBindings = new ActorBindings();
        }
        public void RegisterPrefab<T>(int archetypeId, T prefab) where T : MonoBehaviour, IActor
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            if (actorPools.ContainArchetype(archetypeId))
                throw new InvalidOperationException($"Prefab for pool {archetypeId} is already registered.");

            actorPools.AddPool(archetypeId, prefab);
        }
        public void InstantiateActors<T>(int archetypeId, int capacity) where T : IActor
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            UnityEngine.Object prefab = actorPools.GetPrefab(archetypeId);
            Transform poolRoot = actorPools.GetParent(archetypeId);

            for (int i = 0; i < capacity; i++)
            {
                GameObject instance = InstantiateActorGameObject(archetypeId, prefab, poolRoot);

                T actorComponent = instance.GetComponent<T>();
                if (actorComponent == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    throw new InvalidOperationException($"Prefab for pool {archetypeId} does not contain an actor component of type {typeof(T).Name}.");
                }

                if (!instance.TryGetComponent<UnityActorBindingTag>(out UnityActorBindingTag bindingTag))
                {
                    bindingTag = instance.AddComponent<UnityActorBindingTag>();
                }

                instance.SetActive(false);
                actorPools.Add(archetypeId, actorComponent, instance, bindingTag);
            }
        }
        GameObject InstantiateActorGameObject(int archetypeId, UnityEngine.Object prefab, Transform parent)
        {
            UnityEngine.Object clonedObject = UnityEngine.Object.Instantiate(prefab, parent);
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
                throw new InvalidOperationException($"Prefab for pool {archetypeId} must instantiate as a GameObject or Component.");
            }

            return instance;
        }

        public ActorHandle ActiveAndBindActor(EntityHandle entity, int archetypeId, int slotId)
        {
            if (!actorPools.ContainArchetype(archetypeId))
            {
                throw new InvalidOperationException($"Actor pool for archetype {archetypeId} does not exist.");
            }

            if (!actorPools.InPoolRange(archetypeId, slotId))
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot ID {slotId} is out of range for pool {archetypeId}.");
            }

            actorPools.GetActor(archetypeId, slotId, out IActor actor, out GameObject gameObject, out UnityActorBindingTag bindingTag);

            // Create a binding and store it
            ActorHandle handle = new ActorHandle(archetypeId, slotId);
            ActorBinding binding = actorBindings.Bind(entity, handle);

            // Activate the actor instance
            gameObject.SetActive(true);
            bindingTag.SetBinding(binding);

            return handle;
        }

        public ActorBinding GetActiveBinding(int index)
        {
            return actorBindings.GetActiveBinding(index);
        }

        public bool HasBinding(EntityHandle entity)
        {
            return actorBindings.HasBinding(entity);
        }

        public void Unbind(ActorBinding binding)
        {
            if (!actorBindings.Contains(binding))
            {
                throw new InvalidOperationException($"Binding for entity {binding.Entity} does not exist.");
            }

            // Deactivate the actor instance
            actorPools.GetActor(binding.Actor.ArchetypeId, binding.Actor.SlotId, out IActor actor, out GameObject gameObject, out UnityActorBindingTag bindingTag);

            gameObject.SetActive(false);
            bindingTag.Unbind();
            actorBindings.Unbind(binding);
        }

        public IReadOnlyList<GameObject> GetActorGameObjects(int archetypeId)
        {
            return actorPools.GetActorGameObjects(archetypeId);
        }
    }
}
