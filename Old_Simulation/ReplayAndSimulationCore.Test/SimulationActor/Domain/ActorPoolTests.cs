using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ReplayAndSimulationCore.Test.SimulationActor.Domain
{
    public sealed class ActorPoolTests
    {
        [Test]
        public void Constructor_WhenCapacityIsNegative_Throws()
        {
            ActorDomainTypes types = ActorDomainTypes.Load();

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(types.ActorPool, 7, -1));

            Assert.IsInstanceOf<ArgumentOutOfRangeException>(exception.InnerException);
        }

        [Test]
        public void Acquire_WhenSlotsAreAvailable_ReturnsLowestFreeSlot()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 3);

            ActorAcquireSnapshot first = pool.Acquire();
            ActorAcquireSnapshot second = pool.Acquire();

            AssertAcquire(first, hasActor: true, slotId: 0, generation: 1);
            AssertAcquire(second, hasActor: true, slotId: 1, generation: 1);
        }

        [Test]
        public void Acquire_WhenPoolIsFull_ReturnsNoActor()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 1);
            pool.Acquire();

            ActorAcquireSnapshot result = pool.Acquire();

            AssertAcquire(result, hasActor: false, slotId: -1, generation: 0);
        }

        [Test]
        public void Release_WhenSlotIsReacquired_IncrementsGenerationAndReusesLowestFreeSlot()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 2);
            pool.Acquire();
            pool.Acquire();
            pool.Release(slotId: 0);

            ActorAcquireSnapshot result = pool.Acquire();

            AssertAcquire(result, hasActor: true, slotId: 0, generation: 2);
        }

        [Test]
        public void AcquireAt_WhenSlotIsAlreadyActive_Throws()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 2);
            pool.AcquireAt(slotId: 1);

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(() => pool.AcquireAt(slotId: 1));

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            StringAssert.Contains("is not free", exception.InnerException.Message);
        }

        [Test]
        public void Release_WhenSlotIsNotActive_Throws()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 2);

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(() => pool.Release(slotId: 1));

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            StringAssert.Contains("is not active", exception.InnerException.Message);
        }

        [Test]
        public void ActorPools_GetSortedPoolIds_WhenRegisteredOutOfOrder_ReturnsSortedIds()
        {
            ActorPoolsHarness pools = ActorPoolsHarness.Create();

            pools.AddPool("EnemyActor", poolId: 9, capacity: 1);
            pools.AddPool("PlayerActor", poolId: 3, capacity: 1);
            pools.AddPool("ProjectileActor", poolId: 7, capacity: 1);

            CollectionAssert.AreEqual(new[] { 3, 7, 9 }, pools.GetSortedPoolIds());
        }

        [Test]
        public void ActorPool_ReplayingSameAcquireReleaseSequence_ProducesSameSnapshot()
        {
            List<string> expected = RunPoolScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunPoolScenario());
            }
        }

        private static List<string> RunPoolScenario()
        {
            ActorPoolHarness pool = ActorPoolHarness.Create(poolId: 7, capacity: 4);
            List<string> trace = new();

            trace.Add(pool.Acquire().ToString());
            trace.Add(pool.Acquire().ToString());
            pool.Release(slotId: 0);
            trace.Add(pool.Acquire().ToString());
            trace.Add(pool.AcquireAt(slotId: 3).ToString());
            pool.Release(slotId: 1);
            trace.Add(pool.Acquire().ToString());
            trace.Add(pool.Acquire().ToString());
            trace.Add(pool.Acquire().ToString());

            return trace;
        }

        private static void AssertAcquire(
            ActorAcquireSnapshot result,
            bool hasActor,
            int slotId,
            uint generation)
        {
            Assert.AreEqual(hasActor, result.HasActor);
            Assert.AreEqual(slotId, result.SlotId);
            Assert.AreEqual(generation, result.Generation);
        }

        private readonly struct ActorAcquireSnapshot
        {
            internal readonly bool HasActor;
            internal readonly int SlotId;
            internal readonly uint Generation;

            internal ActorAcquireSnapshot(bool hasActor, int slotId, uint generation)
            {
                HasActor = hasActor;
                SlotId = slotId;
                Generation = generation;
            }

            public override string ToString()
            {
                return $"{HasActor}:{SlotId}:{Generation}";
            }
        }

        private sealed class ActorPoolHarness
        {
            private readonly ActorDomainTypes types;
            private readonly object pool;

            private ActorPoolHarness(ActorDomainTypes types, int poolId, int capacity)
            {
                this.types = types;
                pool = Activator.CreateInstance(types.ActorPool, poolId, capacity);
            }

            internal static ActorPoolHarness Create(int poolId, int capacity)
            {
                return new ActorPoolHarness(ActorDomainTypes.Load(), poolId, capacity);
            }

            internal ActorAcquireSnapshot Acquire()
            {
                object result = types.ActorPoolAcquire.Invoke(pool, Array.Empty<object>());
                return types.Snapshot(result);
            }

            internal ActorAcquireSnapshot AcquireAt(int slotId)
            {
                object result = types.ActorPoolAcquireAt.Invoke(pool, new object[] { slotId });
                return types.Snapshot(result);
            }

            internal void Release(int slotId)
            {
                types.ActorPoolRelease.Invoke(pool, new object[] { slotId });
            }
        }

        private sealed class ActorPoolsHarness
        {
            private readonly ActorDomainTypes types;
            private readonly object pools;

            private ActorPoolsHarness(ActorDomainTypes types)
            {
                this.types = types;
                pools = Activator.CreateInstance(types.ActorPools);
            }

            internal static ActorPoolsHarness Create()
            {
                return new ActorPoolsHarness(ActorDomainTypes.Load());
            }

            internal void AddPool(string actorTypeName, int poolId, int capacity)
            {
                Type actorType = DynamicActorTypeFactory.Create(actorTypeName, types.ActorInterface);
                MethodInfo addPool = types.ActorPoolsAddPool.MakeGenericMethod(actorType);
                addPool.Invoke(pools, new object[] { poolId, capacity });
            }

            internal int[] GetSortedPoolIds()
            {
                return (int[])types.ActorPoolsGetSortedPoolIds.Invoke(pools, Array.Empty<object>());
            }
        }

        private sealed class ActorDomainTypes
        {
            private ActorDomainTypes(Assembly[] assemblies)
            {
                ActorPool = RequiredType(assemblies, "SimulationCore.SimulationActor.Domain.ActorPool");
                ActorPools = RequiredType(assemblies, "SimulationCore.SimulationActor.Domain.ActorPools");
                ActorInterface = RequiredType(assemblies, "SimulationCore.SimulationActor.Contract.IActor");
                ActorAcquireResult = RequiredType(assemblies, "SimulationCore.SimulationActor.Domain.ActorAcquireResult");

                ActorPoolAcquire = ActorPool.GetMethod("Acquire");
                ActorPoolAcquireAt = ActorPool.GetMethod("AcquireAt");
                ActorPoolRelease = ActorPool.GetMethod("Release");
                ActorPoolsAddPool = ActorPools.GetMethod("AddPool");
                ActorPoolsGetSortedPoolIds = ActorPools.GetMethod("GetSortedPoolIds");

                AcquireResultHasActor = ActorAcquireResult.GetProperty("HasActor");
                AcquireResultSlotId = ActorAcquireResult.GetProperty("SlotId");
                AcquireResultGeneration = ActorAcquireResult.GetProperty("Generation");
            }

            internal Type ActorPool { get; }
            internal Type ActorPools { get; }
            internal Type ActorInterface { get; }
            internal Type ActorAcquireResult { get; }
            internal MethodInfo ActorPoolAcquire { get; }
            internal MethodInfo ActorPoolAcquireAt { get; }
            internal MethodInfo ActorPoolRelease { get; }
            internal MethodInfo ActorPoolsAddPool { get; }
            internal MethodInfo ActorPoolsGetSortedPoolIds { get; }
            private PropertyInfo AcquireResultHasActor { get; }
            private PropertyInfo AcquireResultSlotId { get; }
            private PropertyInfo AcquireResultGeneration { get; }

            internal static ActorDomainTypes Load()
            {
                return new ActorDomainTypes(AppDomain.CurrentDomain.GetAssemblies());
            }

            internal ActorAcquireSnapshot Snapshot(object result)
            {
                return new ActorAcquireSnapshot(
                    (bool)AcquireResultHasActor.GetValue(result),
                    (int)AcquireResultSlotId.GetValue(result),
                    (uint)AcquireResultGeneration.GetValue(result));
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

        private static class DynamicActorTypeFactory
        {
            private static readonly AssemblyBuilder Assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SimulationActorDomainTestDynamicActors"),
                AssemblyBuilderAccess.Run);

            private static readonly ModuleBuilder Module = Assembly.DefineDynamicModule("SimulationActorDomainTestDynamicActors");
            private static readonly Dictionary<string, Type> Types = new();

            internal static Type Create(string typeName, Type actorInterface)
            {
                string key = $"{actorInterface.AssemblyQualifiedName}:{typeName}";
                if (Types.TryGetValue(key, out Type existingType))
                {
                    return existingType;
                }

                TypeBuilder builder = Module.DefineType(
                    $"ReplayAndSimulationCore.Test.SimulationActor.Domain.Dynamic.{typeName}",
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
