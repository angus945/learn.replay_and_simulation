
using SimulationCore.SimulationActor.Domain;
using SimulationCore.Contracts;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.World.Contract;
using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application.Dto;

namespace SimulationCore.SimulationActor.Application
{
    public sealed class SimulationActors : ISimulationActor
    {
        IActorBindingPort bindingPort;
        IEntityPort entityPort;

        ActorPools actorPools;

        public SimulationActors(IEntityPort entityPort, IActorBindingPort instancePort)
        {
            this.entityPort = entityPort ?? throw new ArgumentNullException(nameof(entityPort));
            this.bindingPort = instancePort ?? throw new ArgumentNullException(nameof(instancePort));
            this.actorPools = new ActorPools();
        }
        public void RegisterActorPool<T>(int archetypeId, int capacity) where T : class, IActor
        {
            bindingPort.CreateActorInstances<T>(archetypeId, capacity);
            actorPools.AddPool<T>(archetypeId, capacity);
        }
        public void ReconcileBeforePhysics()
        {
            ReleaseUnusedActors();
        }
        public void ReconcileAfterStructuralCommit()
        {
            ReleaseUnusedActors();
            AcquireMissingActors();
        }

        List<EntityHandle> requiredEntities = new List<EntityHandle>();
        List<ActorBinding> releaseBuffer = new List<ActorBinding>();
        private void ReleaseUnusedActors()
        {
            requiredEntities.Clear();
            releaseBuffer.Clear();

            for (int i = 0; i < entityPort.EntityCount; i++)
            {
                EntityHandle entity = entityPort.GetEntity(i);
                requiredEntities.Add(entity);
            }

            for (int i = 0; i < bindingPort.ActiveActorCount; i++)
            {
                ActorBinding binding = bindingPort.GetBinding(i);

                if (!requiredEntities.Contains(binding.Entity))
                {
                    releaseBuffer.Add(binding);
                }
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ActorBinding binding = releaseBuffer[i];

                bindingPort.Unbind(binding);
                actorPools.Release(binding.Actor.ArchetypeId, binding.Actor.SlotId);
            }
        }
        private void AcquireMissingActors()
        {
            for (int i = 0; i < entityPort.EntityCount; i++)
            {
                EntityHandle entity = entityPort.GetEntity(i);

                if (bindingPort.HasBinding(entity))
                    continue;

                ActorArchetypeComponent definition = entityPort.GetActorArchetypeComponent(entity);
                ActorAcquireResult result = actorPools.Acquire(definition.ArchetypeId);
                if (!result.HasActor)
                {
                    throw new InvalidOperationException($"Failed to acquire actor for entity {entity} from pool {definition.ArchetypeId}.");
                }
                bindingPort.ActiveAndBindActor(entity, definition.ArchetypeId, result.SlotId);
            }
        }
    }
}
