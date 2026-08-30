using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ReplayAndSimulationCore.Test.SimulationActor.Application
{
    public sealed class SimulationActorsTests
    {
        [Test]
        public void Constructor_WhenEntityPortIsNull_Throws()
        {
            SimulationActorTypes types = SimulationActorTypes.Load();
            object bindingPort = DispatchProxyFactory.Create(
                types.ActorBindingPortInterface,
                (_, __) => null);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(types.SimulationActors, null, bindingPort));

            Assert.IsInstanceOf<ArgumentNullException>(exception.InnerException);
        }

        [Test]
        public void Constructor_WhenBindingPortIsNull_Throws()
        {
            SimulationActorTypes types = SimulationActorTypes.Load();
            object entityPort = DispatchProxyFactory.Create(
                types.EntityPortInterface,
                (_, __) => null);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(types.SimulationActors, entityPort, null));

            Assert.IsInstanceOf<ArgumentNullException>(exception.InnerException);
        }

        [Test]
        public void RegisterActorPool_InstantiatesActors()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();

            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 3);

            CollectionAssert.AreEqual(
                new[] { "instantiate:TestActor:7:3" },
                harness.BindingPort.Trace);
        }

        [Test]
        public void ReconcileAfterStructuralCommit_WhenEntitiesNeedActors_BindsActorsInEntityOrder()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();
            object first = harness.Entity(slotId: 10, sequenceId: 1);
            object second = harness.Entity(slotId: 20, sequenceId: 2);
            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 2);
            harness.EntityPort.SetEntities(
                EntityRecord.For(first, archetypeId: 7),
                EntityRecord.For(second, archetypeId: 7));

            harness.ReconcileAfterStructuralCommit();

            CollectionAssert.AreEqual(
                new[]
                {
                    "instantiate:TestActor:7:2",
                    "bind:10:1:7:0",
                    "bind:20:2:7:1"
                },
                harness.BindingPort.Trace);
            harness.AssertBinding(first, archetypeId: 7, slotId: 0, harness.BindingPort.GetBinding(0));
            harness.AssertBinding(second, archetypeId: 7, slotId: 1, harness.BindingPort.GetBinding(1));
        }

        [Test]
        public void ReconcileBeforePhysics_WhenEntityNoLongerExists_ReleasesBindingWithoutAcquiringMissingActors()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();
            object oldEntity = harness.Entity(slotId: 10, sequenceId: 1);
            object newEntity = harness.Entity(slotId: 20, sequenceId: 2);
            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 1);
            harness.EntityPort.SetEntities(EntityRecord.For(oldEntity, archetypeId: 7));
            harness.ReconcileAfterStructuralCommit();
            harness.BindingPort.Trace.Clear();

            harness.EntityPort.SetEntities(EntityRecord.For(newEntity, archetypeId: 7));
            harness.ReconcileBeforePhysics();

            CollectionAssert.AreEqual(
                new[] { "unbind:10:1:7:0" },
                harness.BindingPort.Trace);
            Assert.AreEqual(0, harness.BindingPort.ActiveActorCount);
            Assert.IsFalse(harness.BindingPort.HasBinding(newEntity));
        }

        [Test]
        public void ReconcileAfterStructuralCommit_WhenActorIsReleased_ReusesLowestFreeSlot()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();
            object first = harness.Entity(slotId: 10, sequenceId: 1);
            object second = harness.Entity(slotId: 20, sequenceId: 2);
            object third = harness.Entity(slotId: 30, sequenceId: 3);
            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 2);
            harness.EntityPort.SetEntities(
                EntityRecord.For(first, archetypeId: 7),
                EntityRecord.For(second, archetypeId: 7));
            harness.ReconcileAfterStructuralCommit();
            harness.BindingPort.Trace.Clear();

            harness.EntityPort.SetEntities(
                EntityRecord.For(second, archetypeId: 7),
                EntityRecord.For(third, archetypeId: 7));
            harness.ReconcileAfterStructuralCommit();

            CollectionAssert.AreEqual(
                new[]
                {
                    "unbind:10:1:7:0",
                    "bind:30:3:7:0"
                },
                harness.BindingPort.Trace);
            harness.AssertBinding(second, archetypeId: 7, slotId: 1, harness.BindingPort.GetBinding(0));
            harness.AssertBinding(third, archetypeId: 7, slotId: 0, harness.BindingPort.GetBinding(1));
        }

        [Test]
        public void ReconcileAfterStructuralCommit_WhenPoolIsExhausted_Throws()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();
            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 1);
            harness.EntityPort.SetEntities(
                EntityRecord.For(harness.Entity(slotId: 10, sequenceId: 1), archetypeId: 7),
                EntityRecord.For(harness.Entity(slotId: 20, sequenceId: 2), archetypeId: 7));

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(() => harness.ReconcileAfterStructuralCommit());

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            StringAssert.Contains("Failed to acquire actor", exception.InnerException.Message);
        }

        [Test]
        public void ReconcileAfterStructuralCommit_ReplayingSameRuntimeSequence_ProducesSameBindingTrace()
        {
            List<string> expected = RunRuntimeScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunRuntimeScenario());
            }
        }

        private static List<string> RunRuntimeScenario()
        {
            SimulationActorHarness harness = SimulationActorHarness.Create();
            object playerA = harness.Entity(slotId: 10, sequenceId: 1);
            object enemyA = harness.Entity(slotId: 20, sequenceId: 2);
            object playerB = harness.Entity(slotId: 30, sequenceId: 3);
            object playerC = harness.Entity(slotId: 40, sequenceId: 4);
            object enemyB = harness.Entity(slotId: 50, sequenceId: 5);
            object playerD = harness.Entity(slotId: 60, sequenceId: 6);
            harness.RegisterActorPool("TestActor", archetypeId: 7, capacity: 3);
            harness.RegisterActorPool("OtherActor", archetypeId: 9, capacity: 2);

            harness.EntityPort.SetEntities(
                EntityRecord.For(playerA, archetypeId: 7),
                EntityRecord.For(enemyA, archetypeId: 9),
                EntityRecord.For(playerB, archetypeId: 7));
            harness.ReconcileAfterStructuralCommit();

            harness.EntityPort.SetEntities(
                EntityRecord.For(enemyA, archetypeId: 9),
                EntityRecord.For(playerB, archetypeId: 7),
                EntityRecord.For(playerC, archetypeId: 7));
            harness.ReconcileBeforePhysics();
            harness.ReconcileAfterStructuralCommit();

            harness.EntityPort.SetEntities(
                EntityRecord.For(playerC, archetypeId: 7),
                EntityRecord.For(enemyB, archetypeId: 9),
                EntityRecord.For(playerD, archetypeId: 7));
            harness.ReconcileAfterStructuralCommit();

            return new List<string>(harness.BindingPort.Trace);
        }

        private readonly struct EntityRecord
        {
            internal readonly object Entity;
            internal readonly int ArchetypeId;

            private EntityRecord(object entity, int archetypeId)
            {
                Entity = entity;
                ArchetypeId = archetypeId;
            }

            internal static EntityRecord For(object entity, int archetypeId)
            {
                return new EntityRecord(entity, archetypeId);
            }
        }

        private sealed class SimulationActorHarness
        {
            private readonly SimulationActorTypes types;
            private readonly Dictionary<string, Type> actorTypes = new();

            private SimulationActorHarness(SimulationActorTypes types)
            {
                this.types = types;
                EntityPort = new RecordingEntityPort(types);
                BindingPort = new RecordingActorBindingPort(types);

                object entityPortProxy = DispatchProxyFactory.Create(
                    types.EntityPortInterface,
                    EntityPort.Invoke);
                object bindingPortProxy = DispatchProxyFactory.Create(
                    types.ActorBindingPortInterface,
                    BindingPort.Invoke);

                Actors = Activator.CreateInstance(types.SimulationActors, entityPortProxy, bindingPortProxy);
            }

            internal RecordingEntityPort EntityPort { get; }
            internal RecordingActorBindingPort BindingPort { get; }
            private object Actors { get; }

            internal static SimulationActorHarness Create()
            {
                return new SimulationActorHarness(SimulationActorTypes.Load());
            }

            internal object Entity(int slotId, ulong sequenceId)
            {
                return Activator.CreateInstance(types.EntityHandle, slotId, sequenceId);
            }

            internal void RegisterActorPool(string actorTypeName, int archetypeId, int capacity)
            {
                Type actorType = GetActorType(actorTypeName);
                MethodInfo method = types.SimulationActors
                    .GetMethod("RegisterActorPool")
                    .MakeGenericMethod(actorType);

                method.Invoke(Actors, new object[] { archetypeId, capacity });
            }

            internal void ReconcileBeforePhysics()
            {
                types.SimulationActors
                    .GetMethod("ReconcileBeforePhysics")
                    .Invoke(Actors, Array.Empty<object>());
            }

            internal void ReconcileAfterStructuralCommit()
            {
                types.SimulationActors
                    .GetMethod("ReconcileAfterStructuralCommit")
                    .Invoke(Actors, Array.Empty<object>());
            }

            internal void AssertBinding(object entity, int archetypeId, int slotId, object binding)
            {
                Assert.AreEqual(entity, types.ActorBindingEntity.GetValue(binding));

                object actor = types.ActorBindingActor.GetValue(binding);
                Assert.AreEqual(archetypeId, types.ActorHandleArchetypeId.GetValue(actor));
                Assert.AreEqual(slotId, types.ActorHandleSlotId.GetValue(actor));
            }

            private Type GetActorType(string typeName)
            {
                if (!actorTypes.TryGetValue(typeName, out Type actorType))
                {
                    actorType = DynamicActorTypeFactory.Create(typeName, types.ActorInterface);
                    actorTypes.Add(typeName, actorType);
                }

                return actorType;
            }
        }

        private sealed class RecordingEntityPort
        {
            private readonly SimulationActorTypes types;
            private readonly List<EntityRecord> entities = new();

            internal RecordingEntityPort(SimulationActorTypes types)
            {
                this.types = types;
            }

            internal object Invoke(MethodInfo method, object[] args)
            {
                switch (method.Name)
                {
                    case "get_EntityCount":
                        return entities.Count;
                    case "GetEntity":
                        return entities[(int)args[0]].Entity;
                    case "GetActorArchetypeComponent":
                        return GetActorArchetypeComponent(args[0]);
                    default:
                        throw new NotSupportedException(method.Name);
                }
            }

            internal void SetEntities(params EntityRecord[] records)
            {
                entities.Clear();
                entities.AddRange(records);
            }

            private object GetActorArchetypeComponent(object entity)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    if (entities[i].Entity.Equals(entity))
                    {
                        return Activator.CreateInstance(
                            types.ActorArchetypeComponent,
                            entities[i].ArchetypeId);
                    }
                }

                throw new InvalidOperationException($"Entity {entity} does not have an actor archetype.");
            }
        }

        private sealed class RecordingActorBindingPort
        {
            private readonly SimulationActorTypes types;
            private readonly List<object> activeBindings = new();
            private readonly Dictionary<string, object> bindingsByEntity = new();

            internal RecordingActorBindingPort(SimulationActorTypes types)
            {
                this.types = types;
            }

            internal List<string> Trace { get; } = new();
            internal int ActiveActorCount => activeBindings.Count;

            internal object Invoke(MethodInfo method, object[] args)
            {
                switch (method.Name)
                {
                    case "InstantiateActors":
                        Trace.Add($"instantiate:{method.GetGenericArguments()[0].Name}:{args[0]}:{args[1]}");
                        return null;
                    case "ActiveAndBindActor":
                        return ActiveAndBindActor(args[0], (int)args[1], (int)args[2]);
                    case "get_ActiveActorCount":
                        return activeBindings.Count;
                    case "GetActiveBinding":
                        return GetBinding((int)args[0]);
                    case "HasBinding":
                        return HasBinding(args[0]);
                    case "Unbind":
                        Unbind(args[0]);
                        return null;
                    default:
                        throw new NotSupportedException(method.Name);
                }
            }

            internal object GetBinding(int index)
            {
                return activeBindings[index];
            }

            internal bool HasBinding(object entity)
            {
                return bindingsByEntity.ContainsKey(entity.ToString());
            }

            private object ActiveAndBindActor(object entity, int archetypeId, int slotId)
            {
                object actor = Activator.CreateInstance(types.ActorHandle, archetypeId, slotId);
                object binding = Activator.CreateInstance(types.ActorBinding, entity, actor);

                activeBindings.Add(binding);
                bindingsByEntity.Add(entity.ToString(), binding);
                Trace.Add($"bind:{entity}:{archetypeId}:{slotId}");

                return actor;
            }

            private void Unbind(object binding)
            {
                object entity = types.ActorBindingEntity.GetValue(binding);
                object actor = types.ActorBindingActor.GetValue(binding);
                int archetypeId = (int)types.ActorHandleArchetypeId.GetValue(actor);
                int slotId = (int)types.ActorHandleSlotId.GetValue(actor);

                bindingsByEntity.Remove(entity.ToString());
                int index = activeBindings.FindIndex(
                    active => types.ActorBindingEntity.GetValue(active).Equals(entity));
                if (index >= 0)
                {
                    activeBindings.RemoveAt(index);
                }

                Trace.Add($"unbind:{entity}:{archetypeId}:{slotId}");
            }
        }

        private sealed class SimulationActorTypes
        {
            private SimulationActorTypes(Assembly[] assemblies)
            {
                SimulationActors = RequiredType(assemblies, "SimulationCore.SimulationActor.Application.SimulationActors");
                EntityPortInterface = RequiredType(assemblies, "SimulationCore.SimulationActor.Application.IEntityPort");
                ActorBindingPortInterface = RequiredType(assemblies, "SimulationCore.SimulationActor.Application.Port.IActorBindingPort");
                ActorInterface = RequiredType(assemblies, "SimulationCore.SimulationActor.Contract.IActor");
                ActorArchetypeComponent = RequiredType(assemblies, "SimulationCore.SimulationActor.Contract.ActorArchetypeComponent");
                EntityHandle = RequiredType(assemblies, "SimulationCore.World.Contract.EntityHandle");
                ActorHandle = RequiredType(assemblies, "SimulationCore.SimulationActor.Application.Dto.ActorHandle");
                ActorBinding = RequiredType(assemblies, "SimulationCore.SimulationActor.Application.Dto.ActorBinding");

                ActorBindingEntity = ActorBinding.GetProperty("Entity");
                ActorBindingActor = ActorBinding.GetProperty("Actor");
                ActorHandleArchetypeId = ActorHandle.GetProperty("ArchetypeId");
                ActorHandleSlotId = ActorHandle.GetProperty("SlotId");
            }

            internal Type SimulationActors { get; }
            internal Type EntityPortInterface { get; }
            internal Type ActorBindingPortInterface { get; }
            internal Type ActorInterface { get; }
            internal Type ActorArchetypeComponent { get; }
            internal Type EntityHandle { get; }
            internal Type ActorHandle { get; }
            internal Type ActorBinding { get; }
            internal PropertyInfo ActorBindingEntity { get; }
            internal PropertyInfo ActorBindingActor { get; }
            internal PropertyInfo ActorHandleArchetypeId { get; }
            internal PropertyInfo ActorHandleSlotId { get; }

            internal static SimulationActorTypes Load()
            {
                return new SimulationActorTypes(AppDomain.CurrentDomain.GetAssemblies());
            }

            private static Type RequiredType(Assembly[] assemblies, string typeName)
            {
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type type = assemblies[i].GetType(typeName);
                    if (type != null)
                    {
                        return type;
                    }
                }

                Assert.Fail($"Type {typeName} was not found in loaded assemblies.");
                return null;
            }
        }

        public class RuntimeDispatchProxy : DispatchProxy
        {
            public RuntimeDispatchProxy()
            {
            }

            internal Func<MethodInfo, object[], object> Handler { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return Handler(targetMethod, args ?? Array.Empty<object>());
            }
        }

        private static class DispatchProxyFactory
        {
            private static readonly MethodInfo CreateMethod = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);

            internal static object Create(Type interfaceType, Func<MethodInfo, object[], object> handler)
            {
                object proxy = CreateMethod
                    .MakeGenericMethod(interfaceType, typeof(RuntimeDispatchProxy))
                    .Invoke(null, null);

                ((RuntimeDispatchProxy)proxy).Handler = handler;
                return proxy;
            }
        }

        private static class DynamicActorTypeFactory
        {
            private static readonly AssemblyBuilder Assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SimulationActorTestDynamicActors"),
                AssemblyBuilderAccess.Run);

            private static readonly ModuleBuilder Module = Assembly.DefineDynamicModule("SimulationActorTestDynamicActors");
            private static readonly Dictionary<string, Type> Types = new();

            internal static Type Create(string typeName, Type actorInterface)
            {
                string key = $"{actorInterface.AssemblyQualifiedName}:{typeName}";
                if (Types.TryGetValue(key, out Type existingType))
                {
                    return existingType;
                }

                TypeBuilder builder = Module.DefineType(
                    $"ReplayAndSimulationCore.Test.SimulationActor.Dynamic.{typeName}",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);

                builder.AddInterfaceImplementation(actorInterface);
                builder.DefineDefaultConstructor(MethodAttributes.Public);

                Type actorType = builder.CreateType();
                Types.Add(key, actorType);

                return actorType;
            }
        }
    }
}
