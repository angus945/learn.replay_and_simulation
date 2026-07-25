using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Infrastructure;
using SimulationCore.World.Contract;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReplayAndSimulationCore.Unity.Test.SimulationActor
{
    public sealed class UnityActorInstancePortPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [UnityTest]
        public IEnumerator CreateActorInstances_WhenPrefabIsRegistered_InstantiatesInactiveActorsUnderPoolRoot()
        {
            UnityActorInstancePort port = CreatePort("CreateActorInstances", out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab("CreateActorInstances_Prefab");

            port.RegisterPrefab(7, prefab);
            port.CreateActorInstances<UnityActorInstancePortTestActor>(7, 3);

            yield return null;

            Transform poolRoot = poolsRoot.Find("ActorPool_7");
            Assert.IsNotNull(poolRoot);
            Assert.AreEqual(3, poolRoot.childCount);
            Assert.AreEqual(0, port.ActiveActorCount);

            for (int i = 0; i < poolRoot.childCount; i++)
            {
                GameObject instance = poolRoot.GetChild(i).gameObject;

                Assert.IsFalse(instance.activeSelf);
                Assert.IsNotNull(instance.GetComponent<UnityActorInstancePortTestActor>());
            }
        }

        [UnityTest]
        public IEnumerator ActiveAndBindActor_WhenSlotExists_ActivatesInstanceAndStoresBinding()
        {
            UnityActorInstancePort port = CreatePort("ActiveAndBindActor", out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab("ActiveAndBindActor_Prefab");
            EntityHandle entity = new EntityHandle(42, 9);

            port.RegisterPrefab(7, prefab);
            port.CreateActorInstances<UnityActorInstancePortTestActor>(7, 2);
            ActorHandle actor = port.ActiveAndBindActor(entity, archetypeId: 7, slotId: 1);

            yield return null;

            Transform poolRoot = poolsRoot.Find("ActorPool_7");
            ActorBinding binding = port.GetBinding(1);

            Assert.AreEqual(7, actor.ArchetypeId);
            Assert.AreEqual(1, actor.SlotId);
            Assert.AreEqual(1, port.ActiveActorCount);
            Assert.IsTrue(port.HasBinding(entity));
            Assert.AreEqual(entity, binding.Entity);
            Assert.AreEqual(actor.ArchetypeId, binding.Actor.ArchetypeId);
            Assert.AreEqual(actor.SlotId, binding.Actor.SlotId);
            Assert.IsFalse(poolRoot.GetChild(0).gameObject.activeSelf);
            Assert.IsTrue(poolRoot.GetChild(1).gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Unbind_WhenBindingExists_DeactivatesInstanceAndRemovesBinding()
        {
            UnityActorInstancePort port = CreatePort("Unbind", out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab("Unbind_Prefab");
            EntityHandle entity = new EntityHandle(42, 9);

            port.RegisterPrefab(7, prefab);
            port.CreateActorInstances<UnityActorInstancePortTestActor>(7, 2);
            port.ActiveAndBindActor(entity, archetypeId: 7, slotId: 1);
            ActorBinding binding = port.GetBinding(1);
            port.Unbind(binding);

            yield return null;

            Assert.AreEqual(0, port.ActiveActorCount);
            Assert.IsFalse(port.HasBinding(entity));
            Assert.IsFalse(poolsRoot.Find("ActorPool_7").GetChild(1).gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator RuntimeSequence_WhenReplayed_ProducesSameActorPoolSnapshot()
        {
            List<string> expected = RunRuntimeScenario("RuntimeSequence_A");

            yield return null;

            List<string> actual = RunRuntimeScenario("RuntimeSequence_B");

            yield return null;

            CollectionAssert.AreEqual(expected, actual);
        }

        private List<string> RunRuntimeScenario(string prefix)
        {
            UnityActorInstancePort port = CreatePort(prefix, out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab($"{prefix}_Prefab");
            EntityHandle first = new EntityHandle(10, 1);
            EntityHandle second = new EntityHandle(20, 2);
            EntityHandle third = new EntityHandle(30, 3);
            EntityHandle fourth = new EntityHandle(40, 4);
            List<string> snapshots = new();

            port.RegisterPrefab(7, prefab);
            port.CreateActorInstances<UnityActorInstancePortTestActor>(7, 3);
            snapshots.Add(Snapshot(port, poolsRoot, "create"));

            port.ActiveAndBindActor(first, archetypeId: 7, slotId: 0);
            port.ActiveAndBindActor(second, archetypeId: 7, slotId: 2);
            snapshots.Add(Snapshot(port, poolsRoot, "bind-first-second"));

            port.Unbind(port.GetBinding(0));
            port.ActiveAndBindActor(third, archetypeId: 7, slotId: 0);
            snapshots.Add(Snapshot(port, poolsRoot, "replace-first"));

            port.Unbind(port.GetBinding(2));
            port.ActiveAndBindActor(fourth, archetypeId: 7, slotId: 1);
            snapshots.Add(Snapshot(port, poolsRoot, "replace-second"));

            return snapshots;
        }

        private static string Snapshot(UnityActorInstancePort port, Transform poolsRoot, string label)
        {
            List<string> parts = new() { label, $"active:{port.ActiveActorCount}" };
            Transform poolRoot = poolsRoot.Find("ActorPool_7");

            for (int i = 0; i < poolRoot.childCount; i++)
            {
                parts.Add($"slot:{i}:active:{poolRoot.GetChild(i).gameObject.activeSelf}");
                parts.Add(GetBindingSnapshot(port, i));
            }

            return string.Join("|", parts);
        }

        private static string GetBindingSnapshot(UnityActorInstancePort port, int slotId)
        {
            try
            {
                ActorBinding binding = port.GetBinding(slotId);
                return $"binding:{slotId}:{binding.Entity}:{binding.Actor.ArchetypeId}:{binding.Actor.SlotId}";
            }
            catch (System.InvalidOperationException)
            {
                return $"binding:{slotId}:none";
            }
        }

        private UnityActorInstancePort CreatePort(string name, out Transform poolsRoot)
        {
            GameObject root = CreateTrackedGameObject($"{name}_PoolsRoot");
            poolsRoot = root.transform;

            return new UnityActorInstancePort(poolsRoot);
        }

        private UnityActorInstancePortTestActor CreatePrefab(string name)
        {
            GameObject prefab = CreateTrackedGameObject(name);

            return prefab.AddComponent<UnityActorInstancePortTestActor>();
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);

            return gameObject;
        }
    }

    public sealed class UnityActorInstancePortTestActor : MonoBehaviour, IActor
    {
    }
}
