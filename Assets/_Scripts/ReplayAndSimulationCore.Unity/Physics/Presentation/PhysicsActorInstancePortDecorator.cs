using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Infrastructure;
using SimulationCore.SimulationActor.Presentation;
using SimulationCore.SimulationPhysics.Application;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using SimulationCore.World.Contract;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Infrastructure
{
    public class PhysicsActorInstancePortDecorator : IActorBindingPort
    {
        UnityActorInstancePort inner;

        ICollisionEventSink collisionEventSink;
        SortedDictionary<int, bool> archetypeWithEvent = new SortedDictionary<int, bool>();

        public PhysicsActorInstancePortDecorator(UnityActorInstancePort inner, ICollisionEventSink collisionEventSink)
        {
            this.inner = inner;
            this.collisionEventSink = collisionEventSink;
        }

        public int ActiveActorCount => inner.ActiveActorCount;

        public void InstantiateActors<T>(int archetypeId, int capacity) where T : IActor
        {
            inner.InstantiateActors<T>(archetypeId, capacity);

            IReadOnlyList<GameObject> actorGameObjects = inner.GetActorGameObjects(archetypeId);
            if (!actorGameObjects[0].TryGetComponent<IUnityCollisionRecordPort>(out _))
            {
                archetypeWithEvent.Add(archetypeId, false);
            }
            else
            {
                archetypeWithEvent.Add(archetypeId, true);
                for (int i = 0; i < actorGameObjects.Count; i++)
                {
                    GameObject actorGameObject = actorGameObjects[i];
                    IUnityCollisionRecordPort[] unityCollisionEvents = actorGameObject.GetComponents<IUnityCollisionRecordPort>();
                    foreach (var unityCollisionEvent in unityCollisionEvents)
                    {
                        unityCollisionEvent.Initial(RecordCollision);
                    }
                }
            }
        }
        void RecordCollision(GameObject actorA, GameObject actorB, ContactPhase contactPhase)
        {
            EntityHandle entityA;
            EntityHandle entityB;
            if (actorA.TryGetComponent(out IActorBindingTag actorBindingTagA))
            {
                entityA = actorBindingTagA.GetBinding().Entity;
            }
            else entityA = EntityHandle.NotEntity;
            if (actorB.TryGetComponent(out IActorBindingTag actorBindingTagB))
            {
                entityB = actorBindingTagB.GetBinding().Entity;
            }
            else entityB = EntityHandle.NotEntity;

            if (entityA == EntityHandle.NotEntity && entityB == EntityHandle.NotEntity)
            {
                throw new InvalidOperationException($"Both actors {actorA.name} and {actorB.name} do not have a binding tag.");
            }

            CollisionFact collisionFact = new CollisionFact(entityA, entityB, contactPhase);
            collisionEventSink.RecordCollision(collisionFact);
        }

        public ActorHandle ActiveAndBindActor(EntityHandle entity, int archetypeId, int slotId)
        {
            return inner.ActiveAndBindActor(entity, archetypeId, slotId); ;
        }
        public ActorBinding GetActiveBinding(int slotId)
        {
            return inner.GetActiveBinding(slotId);
        }

        public bool HasBinding(EntityHandle entity)
        {
            return inner.HasBinding(entity);
        }

        public void Unbind(ActorBinding binding)
        {
            inner.Unbind(binding);
        }
    }
}

