using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Infrastructure;
using SimulationCore.World.Contract;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReplayAndSimulationCore.Unity.Test.Physics
{
    public sealed class UnityPhysicsCollisionEventPlayModeTests
    {
        private const int ArchetypeId = 77;
        private const float StepDeltaTime = 0.02f;

        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private SimulationMode previousSimulationMode;

        [SetUp]
        public void SetUp()
        {
            previousSimulationMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Physics.simulationMode = previousSimulationMode;

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [UnityTest]
        public IEnumerator CollisionEventFlow_WhenBoundActorsCollide_RecordsEnterStayAndExitFacts()
        {
            PhysicsReflection physics = PhysicsReflection.Load();
            CollisionFlowHarness harness = CreateHarness(
                physics,
                "CollisionEventFlow",
                isTrigger: false,
                physics.OnCollisionEnterEvent,
                physics.OnCollisionStayEvent,
                physics.OnCollisionExitEvent);
            EntityHandle entityA = new EntityHandle(100, 1);
            EntityHandle entityB = new EntityHandle(200, 2);

            GameObject actorA = harness.Activate(entityA, slotId: 0, new Vector3(0f, 0f, 0f));
            GameObject actorB = harness.Activate(entityB, slotId: 1, new Vector3(0.25f, 0f, 0f));
            ConfigureBody(actorA, isDynamic: true);
            ConfigureBody(actorB, isDynamic: false);

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Enter"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Enter");

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Stay"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Stay");

            actorB.transform.position = new Vector3(5f, 0f, 0f);

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Exit"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Exit");
        }

        [UnityTest]
        public IEnumerator TriggerEventFlow_WhenBoundActorsOverlap_RecordsEnterStayAndExitFacts()
        {
            PhysicsReflection physics = PhysicsReflection.Load();
            CollisionFlowHarness harness = CreateHarness(
                physics,
                "TriggerEventFlow",
                isTrigger: true,
                physics.OnTriggerEnterEvent,
                physics.OnTriggerStayEvent,
                physics.OnTriggerExitEvent);
            EntityHandle entityA = new EntityHandle(300, 3);
            EntityHandle entityB = new EntityHandle(400, 4);

            GameObject actorA = harness.Activate(entityA, slotId: 0, new Vector3(0f, 0f, 0f));
            GameObject actorB = harness.Activate(entityB, slotId: 1, new Vector3(0.25f, 0f, 0f));
            ConfigureBody(actorA, isDynamic: true);
            ConfigureBody(actorB, isDynamic: false);

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Enter"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Enter");

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Stay"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Stay");

            actorB.transform.position = new Vector3(5f, 0f, 0f);

            yield return SimulateUntil(() => harness.HasFactBetween(entityA, entityB, "Exit"), 10);
            AssertHasFactBetween(harness, entityA, entityB, "Exit");
        }

        private CollisionFlowHarness CreateHarness(
            PhysicsReflection physics,
            string name,
            bool isTrigger,
            params Type[] eventTypes)
        {
            GameObject root = CreateTrackedGameObject($"{name}_PoolsRoot");
            GameObject prefabObject = CreateTrackedGameObject($"{name}_Prefab");
            UnityPhysicsCollisionEventTestActor prefab = prefabObject.AddComponent<UnityPhysicsCollisionEventTestActor>();
            BoxCollider collider = prefabObject.AddComponent<BoxCollider>();
            Rigidbody rigidbody = prefabObject.AddComponent<Rigidbody>();
            UnityActorInstancePort inner = new UnityActorInstancePort(root.transform);
            object sink = Activator.CreateInstance(physics.PhysicsEventSink, new RecordingCommandContext());
            object decorator = Activator.CreateInstance(physics.PhysicsActorInstancePortDecorator, inner, sink);

            collider.isTrigger = isTrigger;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            for (int i = 0; i < eventTypes.Length; i++)
            {
                prefabObject.AddComponent(eventTypes[i]);
            }

            prefabObject.SetActive(false);
            inner.RegisterPrefab(ArchetypeId, prefab);
            physics.InstantiateActors(decorator, typeof(UnityPhysicsCollisionEventTestActor), ArchetypeId, 2);

            return new CollisionFlowHarness(physics, inner, decorator, sink);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);

            return gameObject;
        }

        private static void ConfigureBody(GameObject actor, bool isDynamic)
        {
            Rigidbody rigidbody = actor.GetComponent<Rigidbody>();

            rigidbody.useGravity = false;
            rigidbody.isKinematic = !isDynamic;
            rigidbody.detectCollisions = true;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        private static IEnumerator SimulateUntil(Func<bool> condition, int maxSteps)
        {
            for (int i = 0; i < maxSteps && !condition(); i++)
            {
                UnityEngine.Physics.SyncTransforms();
                UnityEngine.Physics.Simulate(StepDeltaTime);
                yield return null;
            }
        }

        private static void AssertHasFactBetween(
            CollisionFlowHarness harness,
            EntityHandle entityA,
            EntityHandle entityB,
            string phase)
        {
            if (harness.HasFactBetween(entityA, entityB, phase))
            {
                return;
            }

            Assert.Fail($"Expected {phase} collision fact between {entityA} and {entityB}. Actual facts: {harness.DescribeFacts()}");
        }

        private sealed class CollisionFlowHarness
        {
            private readonly object decorator;
            private readonly UnityActorInstancePort inner;
            private readonly PhysicsReflection physics;
            private readonly object sink;

            public CollisionFlowHarness(
                PhysicsReflection physics,
                UnityActorInstancePort inner,
                object decorator,
                object sink)
            {
                this.physics = physics;
                this.inner = inner;
                this.decorator = decorator;
                this.sink = sink;
            }

            public GameObject Activate(EntityHandle entity, int slotId, Vector3 position)
            {
                physics.ActiveAndBindActor(decorator, entity, ArchetypeId, slotId);

                GameObject actor = inner.GetActorGameObjects(ArchetypeId)[slotId];
                actor.transform.position = position;

                return actor;
            }

            public bool HasFactBetween(EntityHandle entityA, EntityHandle entityB, string phase)
            {
                IReadOnlyList<object> facts = physics.GetPendingCollisionFacts(sink);

                for (int i = 0; i < facts.Count; i++)
                {
                    EntityHandle recordedA = physics.GetEntityA(facts[i]);
                    EntityHandle recordedB = physics.GetEntityB(facts[i]);
                    string recordedPhase = physics.GetPhaseName(facts[i]);

                    if (recordedPhase == phase &&
                        ((recordedA == entityA && recordedB == entityB) ||
                         (recordedA == entityB && recordedB == entityA)))
                    {
                        return true;
                    }
                }

                return false;
            }

            public string DescribeFacts()
            {
                IReadOnlyList<object> facts = physics.GetPendingCollisionFacts(sink);
                List<string> descriptions = new List<string>();

                for (int i = 0; i < facts.Count; i++)
                {
                    descriptions.Add($"{physics.GetPhaseName(facts[i])}:{physics.GetEntityA(facts[i])}->{physics.GetEntityB(facts[i])}");
                }

                return string.Join(", ", descriptions);
            }
        }

        private sealed class PhysicsReflection
        {
            private readonly PropertyInfo collisionFactsProperty;
            private readonly FieldInfo entityAField;
            private readonly FieldInfo entityBField;
            private readonly FieldInfo phaseField;
            private readonly MethodInfo activeAndBindActorMethod;
            private readonly MethodInfo instantiateActorsMethod;

            private PhysicsReflection(Assembly assembly)
            {
                PhysicsEventSink = RequireType(assembly, "SimulationCore.SimulationPhysics.Infrastructure.PhysicsEventSink");
                PhysicsActorInstancePortDecorator = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Infrastructure.PhysicsActorInstancePortDecorator");
                CollisionFact = RequireType(assembly, "SimulationCore.SimulationPhysics.Contract.CollisionFact");
                OnCollisionEnterEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnCollisionEnterEvent");
                OnCollisionStayEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnCollisionStayEvent");
                OnCollisionExitEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnCollisionExitEvent");
                OnTriggerEnterEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnTriggerEnterEvent");
                OnTriggerStayEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnTriggerStayEvent");
                OnTriggerExitEvent = RequireType(assembly, "SimulationCore.Unity.PhysicsRuntime.Presentation.OnTriggerExitEvent");

                collisionFactsProperty = RequireProperty(PhysicsEventSink, "CollisionFacts", BindingFlags.Public | BindingFlags.Instance);
                entityAField = RequireField(CollisionFact, "EntityA", BindingFlags.Public | BindingFlags.Instance);
                entityBField = RequireField(CollisionFact, "EntityB", BindingFlags.Public | BindingFlags.Instance);
                phaseField = RequireField(CollisionFact, "Phase", BindingFlags.Public | BindingFlags.Instance);
                activeAndBindActorMethod = RequireMethod(
                    PhysicsActorInstancePortDecorator,
                    "ActiveAndBindActor",
                    typeof(EntityHandle),
                    typeof(int),
                    typeof(int));
                instantiateActorsMethod = RequireMethod(
                    PhysicsActorInstancePortDecorator,
                    "InstantiateActors",
                    typeof(int),
                    typeof(int));
            }

            public Type PhysicsEventSink { get; }
            public Type PhysicsActorInstancePortDecorator { get; }
            public Type CollisionFact { get; }
            public Type OnCollisionEnterEvent { get; }
            public Type OnCollisionStayEvent { get; }
            public Type OnCollisionExitEvent { get; }
            public Type OnTriggerEnterEvent { get; }
            public Type OnTriggerStayEvent { get; }
            public Type OnTriggerExitEvent { get; }

            public static PhysicsReflection Load()
            {
                return new PhysicsReflection(RequireAssembly("Assembly-CSharp"));
            }

            public void InstantiateActors(object decorator, Type actorType, int archetypeId, int capacity)
            {
                Invoke(instantiateActorsMethod.MakeGenericMethod(actorType), decorator, archetypeId, capacity);
            }

            public void ActiveAndBindActor(object decorator, EntityHandle entity, int archetypeId, int slotId)
            {
                Invoke(activeAndBindActorMethod, decorator, entity, archetypeId, slotId);
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
                return (EntityHandle)entityAField.GetValue(collisionFact);
            }

            public EntityHandle GetEntityB(object collisionFact)
            {
                return (EntityHandle)entityBField.GetValue(collisionFact);
            }

            public string GetPhaseName(object collisionFact)
            {
                return phaseField.GetValue(collisionFact).ToString();
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

            private static FieldInfo RequireField(Type type, string name, BindingFlags flags)
            {
                FieldInfo field = type.GetField(name, flags);

                Assert.IsNotNull(field, $"Required field was not found: {type.FullName}.{name}");
                return field;
            }

            private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags)
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
            }
        }
    }

    public sealed class UnityPhysicsCollisionEventTestActor : MonoBehaviour, IActor
    {
    }
}
