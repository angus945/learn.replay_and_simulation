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
        public IEnumerator InstantiateActors_WhenPrefabIsRegistered_InstantiatesInactiveActorsUnderPoolRoot()
        {
            UnityActorInstancePort port = CreatePort("InstantiateActors", out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab("InstantiateActors_Prefab");

            port.RegisterPrefab(7, prefab);
            port.InstantiateActors<UnityActorInstancePortTestActor>(7, 3);

            yield return null;

            Transform poolRoot = GetPoolRoot(poolsRoot, archetypeId: 7, prefab);
            IReadOnlyList<GameObject> instances = port.GetActorGameObjects(7);

            Assert.AreEqual(3, instances.Count);
            Assert.AreEqual(3, poolRoot.childCount);
            Assert.AreEqual(0, port.ActiveActorCount);

            for (int i = 0; i < instances.Count; i++)
            {
                GameObject instance = instances[i];

                Assert.AreSame(poolRoot, instance.transform.parent);
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
            port.InstantiateActors<UnityActorInstancePortTestActor>(7, 2);
            ActorHandle actor = port.ActiveAndBindActor(entity, archetypeId: 7, slotId: 1);

            yield return null;

            Transform poolRoot = GetPoolRoot(poolsRoot, archetypeId: 7, prefab);
            IReadOnlyList<GameObject> instances = port.GetActorGameObjects(7);
            ActorBinding binding = port.GetActiveBinding(0);

            Assert.AreEqual(7, actor.ArchetypeId);
            Assert.AreEqual(1, actor.SlotId);
            Assert.AreEqual(1, port.ActiveActorCount);
            Assert.IsTrue(port.HasBinding(entity));
            Assert.AreEqual(entity, binding.Entity);
            Assert.AreEqual(actor.ArchetypeId, binding.Actor.ArchetypeId);
            Assert.AreEqual(actor.SlotId, binding.Actor.SlotId);
            Assert.AreSame(poolRoot, instances[1].transform.parent);
            Assert.IsFalse(instances[0].activeSelf);
            Assert.IsTrue(instances[1].activeSelf);
        }

        [UnityTest]
        public IEnumerator Unbind_WhenBindingExists_DeactivatesInstanceAndRemovesBinding()
        {
            UnityActorInstancePort port = CreatePort("Unbind", out Transform poolsRoot);
            UnityActorInstancePortTestActor prefab = CreatePrefab("Unbind_Prefab");
            EntityHandle entity = new EntityHandle(42, 9);

            port.RegisterPrefab(7, prefab);
            port.InstantiateActors<UnityActorInstancePortTestActor>(7, 2);
            port.ActiveAndBindActor(entity, archetypeId: 7, slotId: 1);
            ActorBinding binding = port.GetActiveBinding(0);
            port.Unbind(binding);

            yield return null;

            IReadOnlyList<GameObject> instances = port.GetActorGameObjects(7);

            Assert.AreEqual(0, port.ActiveActorCount);
            Assert.IsFalse(port.HasBinding(entity));
            Assert.IsFalse(instances[1].activeSelf);
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
            port.InstantiateActors<UnityActorInstancePortTestActor>(7, 3);
            snapshots.Add(Snapshot(port, poolsRoot, "create"));

            port.ActiveAndBindActor(first, archetypeId: 7, slotId: 0);
            port.ActiveAndBindActor(second, archetypeId: 7, slotId: 2);
            snapshots.Add(Snapshot(port, poolsRoot, "bind-first-second"));

            port.Unbind(GetBindingForSlot(port, slotId: 0));
            port.ActiveAndBindActor(third, archetypeId: 7, slotId: 0);
            snapshots.Add(Snapshot(port, poolsRoot, "replace-first"));

            port.Unbind(GetBindingForSlot(port, slotId: 2));
            port.ActiveAndBindActor(fourth, archetypeId: 7, slotId: 1);
            snapshots.Add(Snapshot(port, poolsRoot, "replace-second"));

            return snapshots;
        }

        private static string Snapshot(UnityActorInstancePort port, Transform poolsRoot, string label)
        {
            List<string> parts = new() { label, $"active:{port.ActiveActorCount}" };
            IReadOnlyList<GameObject> instances = port.GetActorGameObjects(7);

            for (int i = 0; i < instances.Count; i++)
            {
                parts.Add($"slot:{i}:active:{instances[i].activeSelf}");
                parts.Add(GetBindingSnapshot(port, i));
            }

            return string.Join("|", parts);
        }

        private static string GetBindingSnapshot(UnityActorInstancePort port, int slotId)
        {
            for (int i = 0; i < port.ActiveActorCount; i++)
            {
                ActorBinding binding = port.GetActiveBinding(i);
                if (binding.Actor.SlotId == slotId)
                {
                    return $"binding:{slotId}:{binding.Entity}:{binding.Actor.ArchetypeId}:{binding.Actor.SlotId}";
                }
            }

            return $"binding:{slotId}:none";
        }

        private static ActorBinding GetBindingForSlot(UnityActorInstancePort port, int slotId)
        {
            for (int i = 0; i < port.ActiveActorCount; i++)
            {
                ActorBinding binding = port.GetActiveBinding(i);
                if (binding.Actor.SlotId == slotId)
                {
                    return binding;
                }
            }

            Assert.Fail($"Binding for slot {slotId} was not found.");
            return default;
        }

        private static Transform GetPoolRoot(Transform poolsRoot, int archetypeId, Object prefab)
        {
            Transform poolRoot = poolsRoot.Find($"{archetypeId}_{prefab.name}");

            Assert.IsNotNull(poolRoot);
            return poolRoot;
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
