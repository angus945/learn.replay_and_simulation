using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.World.Contract;
using UnityEngine.TestTools;

namespace ReplayAndSimulationCore.Test.Physics
{
    public sealed class SimulationPhysicsPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicsEventSink_WhenCollisionIsRecorded_KeepsPendingCollisionFact()
        {
            PhysicsTypes physicsTypes = PhysicsTypes.Load();
            object sink = Activator.CreateInstance(physicsTypes.PhysicsEventSink, new RecordingCommandContext());
            EntityHandle entityA = new EntityHandle(10, 1);
            EntityHandle entityB = new EntityHandle(20, 2);
            object fact = physicsTypes.CreateCollisionFact(entityA, entityB, "Enter");

            physicsTypes.RecordCollision(sink, fact);

            yield return null;

            IReadOnlyList<object> pendingFacts = physicsTypes.GetPendingCollisionFacts(sink);

            Assert.AreEqual(1, pendingFacts.Count);
            Assert.AreEqual(entityA, physicsTypes.GetEntityA(pendingFacts[0]));
            Assert.AreEqual(entityB, physicsTypes.GetEntityB(pendingFacts[0]));
            Assert.AreEqual("Enter", physicsTypes.GetPhaseName(pendingFacts[0]));
        }

        [UnityTest]
        public IEnumerator PublishPhysicsEvents_WhenCollisionWasRecorded_PublishesAndClearsPendingFacts()
        {
            PhysicsTypes physicsTypes = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = Activator.CreateInstance(physicsTypes.PhysicsEventSink, commandContext);
            object physics = Activator.CreateInstance(physicsTypes.SimulationPhysics, null, sink);
            const ulong tick = 123UL;
            object fact = physicsTypes.CreateCollisionFact(
                new EntityHandle(30, 3),
                new EntityHandle(40, 4),
                "Stay");

            physicsTypes.RecordCollision(sink, fact);

            yield return null;

            Assert.DoesNotThrow(
                () => physicsTypes.PublishPhysicsEvents(physics, tick),
                "PublishPhysicsEvents should publish recorded collision facts instead of throwing.");
            Assert.AreEqual(
                0,
                physicsTypes.GetPendingCollisionFacts(sink).Count,
                "Published collision facts should not remain pending.");
            Assert.AreEqual(1, commandContext.Events.Count);
            Assert.AreEqual(tick, commandContext.Events[0].Metadata.Tick);
            Assert.IsFalse(commandContext.Events[0].Metadata.IsExternal);
            Assert.AreEqual(CommandSource.Physics, commandContext.Events[0].Metadata.Source);
            Assert.AreEqual("SimulationCore.SimulationPhysics.Contract.OnCollisionStay", commandContext.Events[0].Event.GetType().FullName);
            Assert.AreEqual(new EntityHandle(30, 3), physicsTypes.GetEntityA(commandContext.Events[0].Event));
            Assert.AreEqual(new EntityHandle(40, 4), physicsTypes.GetEntityB(commandContext.Events[0].Event));
        }

        private sealed class PhysicsTypes
        {
            private readonly PropertyInfo collisionFactsProperty;
            private readonly ConstructorInfo collisionFactConstructor;
            private readonly MethodInfo publishPhysicsEventsMethod;
            private readonly MethodInfo recordCollisionMethod;

            private PhysicsTypes(
                Type physicsEventSink,
                Type simulationPhysics,
                Type collisionFact,
                Type contactPhase)
            {
                PhysicsEventSink = physicsEventSink;
                SimulationPhysics = simulationPhysics;
                CollisionFact = collisionFact;
                ContactPhase = contactPhase;

                collisionFactsProperty = RequireProperty(PhysicsEventSink, "CollisionFacts");
                collisionFactConstructor = RequireConstructor(CollisionFact, typeof(EntityHandle), typeof(EntityHandle), ContactPhase);
                recordCollisionMethod = RequireMethod(PhysicsEventSink, "RecordCollision", CollisionFact);
                publishPhysicsEventsMethod = RequireMethod(SimulationPhysics, "PublishPhysicsEvents", typeof(ulong));
            }

            public Type PhysicsEventSink { get; }
            public Type SimulationPhysics { get; }
            public Type CollisionFact { get; }
            public Type ContactPhase { get; }

            public static PhysicsTypes Load()
            {
                Assembly assembly = RequireAssembly("Assembly-CSharp");

                return new PhysicsTypes(
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Infrastructure.PhysicsEventSink"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Application.SimulationPhysics"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Contract.CollisionFact"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Contract.ContactPhase"));
            }

            public object CreateCollisionFact(EntityHandle entityA, EntityHandle entityB, string phaseName)
            {
                object phase = Enum.Parse(ContactPhase, phaseName);
                return collisionFactConstructor.Invoke(new[] { (object)entityA, entityB, phase });
            }

            public void RecordCollision(object sink, object collisionFact)
            {
                Invoke(recordCollisionMethod, sink, collisionFact);
            }

            public void PublishPhysicsEvents(object physics, ulong tick)
            {
                Invoke(publishPhysicsEventsMethod, physics, tick);
            }

            public IReadOnlyList<object> GetPendingCollisionFacts(object sink)
            {
                IEnumerable facts = (IEnumerable)collisionFactsProperty.GetValue(sink);
                List<object> result = new List<object>();

                foreach (object fact in facts)
                {
                    result.Add(fact);
                }

                return result;
            }

            public EntityHandle GetEntityA(object collisionFact)
            {
                return GetEntityHandle(collisionFact, "EntityA");
            }

            public EntityHandle GetEntityB(object collisionFact)
            {
                return GetEntityHandle(collisionFact, "EntityB");
            }

            public string GetPhaseName(object collisionFact)
            {
                object phase = RequireField(CollisionFact, "Phase", BindingFlags.Public | BindingFlags.Instance).GetValue(collisionFact);
                return phase.ToString();
            }

            private static EntityHandle GetEntityHandle(object instance, string fieldName)
            {
                return (EntityHandle)RequireField(instance.GetType(), fieldName, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
            }

            private static Assembly RequireAssembly(string assemblyName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == assemblyName)
                    {
                        return assembly;
                    }
                }

                Assert.Fail($"Required assembly was not loaded: {assemblyName}");
                return null;
            }

            private static Type RequireType(Assembly assembly, string fullName)
            {
                Type type = assembly.GetType(fullName);

                Assert.IsNotNull(type, $"Required type was not found: {fullName}");
                return type;
            }

            private static ConstructorInfo RequireConstructor(Type type, params Type[] parameterTypes)
            {
                ConstructorInfo constructor = type.GetConstructor(parameterTypes);

                Assert.IsNotNull(constructor, $"Required constructor was not found on {type.FullName}.");
                return constructor;
            }

            private static FieldInfo RequireField(
                Type type,
                string name,
                BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
            {
                FieldInfo field = type.GetField(name, flags);

                Assert.IsNotNull(field, $"Required field was not found: {type.FullName}.{name}");
                return field;
            }

            private static PropertyInfo RequireProperty(
                Type type,
                string name,
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
            {
                PropertyInfo property = type.GetProperty(name, flags);

                Assert.IsNotNull(property, $"Required property was not found: {type.FullName}.{name}");
                return property;
            }

            private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
            {
                MethodInfo method = type.GetMethod(name, parameterTypes);

                Assert.IsNotNull(method, $"Required method was not found: {type.FullName}.{name}");
                return method;
            }

            private static void Invoke(MethodInfo method, object target, params object[] parameters)
            {
                try
                {
                    method.Invoke(target, parameters);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }
            }
        }

        private sealed class RecordingCommandContext : ICommandContext
        {
            public readonly List<RecordedEvent> Events = new List<RecordedEvent>();

            public void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
            {
            }

            public void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
            {
            }

            public void EnqueueCommand<T>(CommandMetadata data, T command) where T : ICommand
            {
            }

            public void EnqueueEvent<T>(CommandMetadata data, T @event) where T : IEvent
            {
                Events.Add(new RecordedEvent(data, @event));
            }
        }

        private readonly struct RecordedEvent
        {
            public readonly CommandMetadata Metadata;
            public readonly IEvent Event;

            public RecordedEvent(CommandMetadata metadata, IEvent @event)
            {
                Metadata = metadata;
                Event = @event;
            }
        }
    }
}
