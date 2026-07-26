using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.World.Contract;

namespace ReplayAndSimulationCore.Test.Physics.EditMode
{
    [TestFixture]
    public sealed class SimulationPhysicsEditModeTests
    {
        [Test]
        public void CollisionFact_WhenEntitiesAreOutOfOrder_NormalizesBySequenceThenSlot()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            EntityHandle highSequence = new EntityHandle(1, 9);
            EntityHandle lowSequence = new EntityHandle(99, 1);
            EntityHandle highSlot = new EntityHandle(10, 5);
            EntityHandle lowSlot = new EntityHandle(2, 5);

            object factSortedBySequence = physics.CreateCollisionFact(highSequence, lowSequence, "Enter");
            object factSortedBySlot = physics.CreateCollisionFact(highSlot, lowSlot, "Stay");

            Assert.AreEqual(lowSequence, physics.GetEntityA(factSortedBySequence));
            Assert.AreEqual(highSequence, physics.GetEntityB(factSortedBySequence));
            Assert.AreEqual(lowSlot, physics.GetEntityA(factSortedBySlot));
            Assert.AreEqual(highSlot, physics.GetEntityB(factSortedBySlot));
        }

        [Test]
        public void RecordCollision_WhenFactIsRecorded_StoresPendingFactWithoutPublishing()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = physics.CreateSink(commandContext);
            EntityHandle entityA = new EntityHandle(10, 1);
            EntityHandle entityB = new EntityHandle(20, 2);
            object fact = physics.CreateCollisionFact(entityA, entityB, "Enter");

            physics.RecordCollision(sink, fact);

            IReadOnlyList<object> pendingFacts = physics.GetPendingCollisionFacts(sink);
            Assert.AreEqual(1, pendingFacts.Count);
            Assert.AreEqual(entityA, physics.GetEntityA(pendingFacts[0]));
            Assert.AreEqual(entityB, physics.GetEntityB(pendingFacts[0]));
            Assert.AreEqual("Enter", physics.GetPhaseName(pendingFacts[0]));
            Assert.AreEqual(0, commandContext.Events.Count);
        }

        [Test]
        public void PublishCollisionEvents_WhenAllPhasesRecorded_PublishesTypedEventsWithPhysicsMetadata()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = physics.CreateSink(commandContext);
            EntityHandle entityA = new EntityHandle(10, 1);
            EntityHandle entityB = new EntityHandle(20, 2);
            const ulong tick = 777UL;

            physics.RecordCollision(sink, physics.CreateCollisionFact(entityB, entityA, "Exit"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(entityB, entityA, "Stay"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(entityB, entityA, "Enter"));

            physics.PublishCollisionEvents(sink, tick);

            Assert.AreEqual(3, commandContext.Events.Count);
            AssertRecordedEvent(physics, commandContext.Events[0], "OnCollisionEnter", tick, entityA, entityB);
            AssertRecordedEvent(physics, commandContext.Events[1], "OnCollisionStay", tick, entityA, entityB);
            AssertRecordedEvent(physics, commandContext.Events[2], "OnCollisionExit", tick, entityA, entityB);
            Assert.AreEqual(0, physics.GetPendingCollisionFacts(sink).Count);
        }

        [Test]
        public void PublishCollisionEvents_WhenDuplicateFactsRecorded_PublishesOnlyOneEventAndClearsPendingFacts()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = physics.CreateSink(commandContext);
            EntityHandle entityA = new EntityHandle(5, 1);
            EntityHandle entityB = new EntityHandle(7, 3);
            const ulong tick = 9UL;

            physics.RecordCollision(sink, physics.CreateCollisionFact(entityA, entityB, "Enter"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(entityB, entityA, "Enter"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(entityA, entityB, "Enter"));

            physics.PublishCollisionEvents(sink, tick);

            Assert.AreEqual(1, commandContext.Events.Count);
            AssertRecordedEvent(physics, commandContext.Events[0], "OnCollisionEnter", tick, entityA, entityB);
            Assert.AreEqual(0, physics.GetPendingCollisionFacts(sink).Count);
        }

        [Test]
        public void PublishCollisionEvents_WhenFactsRecordedOutOfOrder_PublishesInDeterministicEntityOrder()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = physics.CreateSink(commandContext);
            EntityHandle lowA = new EntityHandle(10, 1);
            EntityHandle lowB = new EntityHandle(20, 1);
            EntityHandle middleA = new EntityHandle(5, 2);
            EntityHandle middleB = new EntityHandle(7, 2);
            EntityHandle highA = new EntityHandle(1, 3);
            EntityHandle highB = new EntityHandle(2, 3);

            physics.RecordCollision(sink, physics.CreateCollisionFact(highA, highB, "Enter"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(middleA, middleB, "Enter"));
            physics.RecordCollision(sink, physics.CreateCollisionFact(lowA, lowB, "Enter"));

            physics.PublishCollisionEvents(sink, 1UL);

            Assert.AreEqual(3, commandContext.Events.Count);
            Assert.AreEqual(lowA, physics.GetEntityA(commandContext.Events[0].Event));
            Assert.AreEqual(middleA, physics.GetEntityA(commandContext.Events[1].Event));
            Assert.AreEqual(highA, physics.GetEntityA(commandContext.Events[2].Event));
        }

        [Test]
        public void SimulationPhysicsPublishPhysicsEvents_WhenCollisionWasRecorded_DelegatesTickToEventPort()
        {
            PhysicsTypes physics = PhysicsTypes.Load();
            RecordingCommandContext commandContext = new RecordingCommandContext();
            object sink = physics.CreateSink(commandContext);
            object simulationPhysics = physics.CreateSimulationPhysics(null, sink);
            EntityHandle entityA = new EntityHandle(30, 3);
            EntityHandle entityB = new EntityHandle(40, 4);
            const ulong tick = 123UL;

            physics.RecordCollision(sink, physics.CreateCollisionFact(entityA, entityB, "Stay"));

            physics.PublishPhysicsEvents(simulationPhysics, tick);

            Assert.AreEqual(1, commandContext.Events.Count);
            AssertRecordedEvent(physics, commandContext.Events[0], "OnCollisionStay", tick, entityA, entityB);
        }

        private static void AssertRecordedEvent(
            PhysicsTypes physics,
            RecordedEvent recordedEvent,
            string eventTypeName,
            ulong tick,
            EntityHandle entityA,
            EntityHandle entityB)
        {
            Assert.AreEqual(eventTypeName, recordedEvent.Event.GetType().Name);
            Assert.AreEqual(tick, recordedEvent.Metadata.Tick);
            Assert.IsFalse(recordedEvent.Metadata.IsExternal);
            Assert.AreEqual(CommandSource.Physics, recordedEvent.Metadata.Source);
            Assert.AreEqual(entityA, physics.GetEntityA(recordedEvent.Event));
            Assert.AreEqual(entityB, physics.GetEntityB(recordedEvent.Event));
        }

        private sealed class PhysicsTypes
        {
            private readonly ConstructorInfo collisionFactConstructor;
            private readonly MethodInfo publishCollisionEventsMethod;
            private readonly MethodInfo publishPhysicsEventsMethod;
            private readonly MethodInfo recordCollisionMethod;
            private readonly PropertyInfo collisionFactsProperty;

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

                collisionFactConstructor = RequireConstructor(CollisionFact, typeof(EntityHandle), typeof(EntityHandle), ContactPhase);
                recordCollisionMethod = RequireMethod(PhysicsEventSink, "RecordCollision", CollisionFact);
                publishCollisionEventsMethod = RequireMethod(PhysicsEventSink, "PublishCollisionEvents", typeof(ulong));
                publishPhysicsEventsMethod = RequireMethod(SimulationPhysics, "PublishPhysicsEvents", typeof(ulong));
                collisionFactsProperty = RequireProperty(PhysicsEventSink, "CollisionFacts");
            }

            public Type PhysicsEventSink { get; }
            public Type SimulationPhysics { get; }
            public Type CollisionFact { get; }
            public Type ContactPhase { get; }

            public static PhysicsTypes Load()
            {
                Assembly assembly = RequireAssemblyContaining(
                    "SimulationCore.SimulationPhysics.Infrastructure.PhysicsEventSink");

                return new PhysicsTypes(
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Infrastructure.PhysicsEventSink"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Application.SimulationPhysics"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Contract.CollisionFact"),
                    RequireType(assembly, "SimulationCore.SimulationPhysics.Contract.ContactPhase"));
            }

            public object CreateSink(RecordingCommandContext commandContext)
            {
                return Activator.CreateInstance(PhysicsEventSink, commandContext);
            }

            public object CreateSimulationPhysics(object simulationPort, object eventPort)
            {
                return Activator.CreateInstance(SimulationPhysics, simulationPort, eventPort);
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

            public void PublishCollisionEvents(object sink, ulong tick)
            {
                Invoke(publishCollisionEventsMethod, sink, tick);
            }

            public void PublishPhysicsEvents(object simulationPhysics, ulong tick)
            {
                Invoke(publishPhysicsEventsMethod, simulationPhysics, tick);
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

            public EntityHandle GetEntityA(object instance)
            {
                return GetEntityHandle(instance, "EntityA");
            }

            public EntityHandle GetEntityB(object instance)
            {
                return GetEntityHandle(instance, "EntityB");
            }

            public string GetPhaseName(object collisionFact)
            {
                object phase = RequireField(CollisionFact, "Phase", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(collisionFact);

                return phase.ToString();
            }

            private static Assembly RequireAssemblyContaining(string fullName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetType(fullName) != null)
                    {
                        return assembly;
                    }
                }

                Assert.Fail($"Required type was not loaded: {fullName}");
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

            private static FieldInfo RequireField(Type type, string name, BindingFlags flags)
            {
                FieldInfo field = type.GetField(name, flags);

                Assert.IsNotNull(field, $"Required field was not found: {type.FullName}.{name}");
                return field;
            }

            private static PropertyInfo RequireProperty(Type type, string name)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

                Assert.IsNotNull(property, $"Required property was not found: {type.FullName}.{name}");
                return property;
            }

            private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
            {
                MethodInfo method = type.GetMethod(name, parameterTypes);

                Assert.IsNotNull(method, $"Required method was not found: {type.FullName}.{name}");
                return method;
            }

            private static EntityHandle GetEntityHandle(object instance, string fieldName)
            {
                return (EntityHandle)RequireField(
                    instance.GetType(),
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
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
