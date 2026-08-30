using System;
using System.Collections;
using System.Collections.Generic;
using DeterministicSimulation.Framework;
using NUnit.Framework;
using SimulationObjects.Contract;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DeterministicSimulation.Unity.Tests
{
    public sealed class LocalPhysicsTests
    {
        [UnityTest]
        public IEnumerator LocalSensorsPublishDeduplicatedFactsWithoutSteppingDefaultPhysics()
        {
            GameObject prefab = CreateSensor();
            GameObject defaultObject = new GameObject("Default-scene body");
            Rigidbody defaultBody = defaultObject.AddComponent<Rigidbody>();
            defaultBody.useGravity = true;
            UnityActorPool pool = new UnityActorPool();
            pool.RegisterPrefab(0, prefab, 2);
            pool.Seal();
            PoseSource source = new PoseSource { Poses = new[] { Pose(9, .25f), Pose(2, 0) } };
            FactSink sink = new FactSink();
            LocalPhysicsParticipant participant = new LocalPhysicsParticipant(pool, source, sink, maxFactsPerTick: 1);
            Scene localScene = participant.Scene;
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 defaultPosition = defaultBody.position;
            try
            {
                Assert.That(localScene.GetPhysicsScene(), Is.Not.EqualTo(Physics.defaultPhysicsScene));
                participant.Simulate(Context(1));
                Assert.That(Physics.simulationMode, Is.EqualTo(previousMode));
                Assert.That(defaultBody.position, Is.EqualTo(defaultPosition), "Local stepping must not advance the default scene.");
                Assert.That(sink.Tick, Is.EqualTo(1));
                Assert.That(sink.Facts.Count, Is.EqualTo(1), "Mirrored and compound-collider callbacks must deduplicate. Observed: " + DescribeFacts(sink.Facts));
                Assert.That(sink.Facts[0].Kind, Is.EqualTo(PhysicsFactKind.TriggerEnter));
                Assert.That(sink.Facts[0].First.Value, Is.EqualTo(2));
                Assert.That(sink.Facts[0].Second.Value, Is.EqualTo(9));
                IReadOnlyList<PhysicsFact> initialFacts = participant.LastFacts;

                pool.TryGetInstance(pool.GetActiveBindings()[0].Instance, out GameObject instance);
                instance.transform.position = Vector3.one * 100;
                participant.Simulate(Context(2));
                Assert.That(instance.transform.position, Is.EqualTo(Vector3.zero), "Physics must restore the logical pose after render interpolation.");
                Assert.That(initialFacts[0].Kind, Is.EqualTo(PhysicsFactKind.TriggerEnter), "Prior fact batches must remain immutable.");
                Assert.Throws<InvalidOperationException>(() => participant.Simulate(Context(2)));
            }
            finally
            {
                participant.Dispose();
                UnityEngine.Object.Destroy(prefab);
                UnityEngine.Object.Destroy(defaultObject);
            }
            for (int frame = 0; frame < 30 && localScene.IsValid() && localScene.isLoaded; frame++) yield return null;
            Assert.That(localScene.IsValid() && localScene.isLoaded, Is.False, "Dispose must unload the owned local scene.");
            Assert.That(Physics.simulationMode, Is.EqualTo(previousMode));
        }

        [UnityTest]
        public IEnumerator ReusedAndForeignBindingsCannotProduceStaleContactFacts()
        {
            GameObject prefab = CreateSensor();
            UnityActorPool pool = new UnityActorPool();
            pool.RegisterPrefab(0, prefab, 2);
            pool.Seal();
            PoseSource source = new PoseSource { Poses = new[] { Pose(2, 0), Pose(9, .25f) } };
            FactSink sink = new FactSink();
            LocalPhysicsParticipant participant = new LocalPhysicsParticipant(pool, source, sink);

            UnityActorPool foreignPool = new UnityActorPool();
            foreignPool.RegisterPrefab(0, prefab, 1);
            foreignPool.Seal();
            PoseSource foreignSource = new PoseSource { Poses = new[] { Pose(77, .25f) } };
            LocalPhysicsParticipant foreignOwner = new LocalPhysicsParticipant(foreignPool, foreignSource, new FactSink());
            GameObject foreignInstance = null;
            GameObject unboundInstance = null;
            try
            {
                participant.Simulate(Context(1));
                Assert.That(sink.Facts.Count, Is.EqualTo(1), "Observed: " + DescribeFacts(sink.Facts));
                Assert.That(sink.Facts[0].First.Value, Is.EqualTo(2));
                InstanceHandle oldHandle = pool.GetActiveBindings()[0].Instance;
                pool.TryGetInstance(oldHandle, out GameObject originalInstance);
                pool.TryGetInstance(pool.GetActiveBindings()[1].Instance, out GameObject survivor);

                source.Poses = new[] { Pose(9, .25f), Pose(42, 100) };
                pool.Reconcile(source.Poses); // Deactivate/rebind before the next physics step.
                ActorBinding replacement = pool.GetActiveBindings()[1];
                Assert.That(replacement.Id.Value, Is.EqualTo(42));
                Assert.That(replacement.Instance.Slot, Is.EqualTo(oldHandle.Slot));
                Assert.That(replacement.Instance.Generation, Is.EqualTo(oldHandle.Generation + 1));
                Assert.That(pool.TryGetInstance(oldHandle, out GameObject stale), Is.False);
                pool.TryGetInstance(replacement.Instance, out GameObject reused);
                Assert.That(reused, Is.SameAs(originalInstance));
                reused.SendMessage("OnTriggerEnter", survivor.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
                Assert.That(sink.Calls, Is.EqualTo(1), "Reconciliation and callbacks outside Simulate cannot publish facts.");

                participant.Simulate(Context(2));
                Assert.That(sink.Facts, Is.Empty,
                    "A delayed old contact must not be relabeled as an Exit/contact of the distant replacement ID.");

                // Deliberately violate scene placement to exercise binding validation, not merely scene isolation.
                foreignOwner.Simulate(Context(1));
                foreignPool.TryGetInstance(foreignPool.GetActiveBindings()[0].Instance, out foreignInstance);
                foreignInstance.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(foreignInstance, participant.Scene);
                TriggerContactProbe foreignProbe = foreignInstance.AddComponent<TriggerContactProbe>();
                unboundInstance = UnityEngine.Object.Instantiate(prefab);
                unboundInstance.transform.position = new Vector3(.25f, 0, 0);
                SceneManager.MoveGameObjectToScene(unboundInstance, participant.Scene);
                TriggerContactProbe unboundProbe = unboundInstance.AddComponent<TriggerContactProbe>();
                unboundInstance.SetActive(true);

                participant.Simulate(Context(3));
                Assert.That(foreignProbe.EnterCount, Is.GreaterThan(0), "Foreign sensor must actually generate native callbacks.");
                Assert.That(unboundProbe.EnterCount, Is.GreaterThan(0), "Unbound sensor must actually generate native callbacks.");
                Assert.That(sink.Facts, Is.Empty, "Foreign-owner and unbound collider callbacks must fail closed.");

                source.Poses = new[] { Pose(9, .25f), Pose(42, .5f) };
                participant.Simulate(Context(4));
                Assert.That(sink.Facts.Count, Is.EqualTo(1), "Observed: " + DescribeFacts(sink.Facts));
                Assert.That(sink.Facts[0].First.Value, Is.EqualTo(9));
                Assert.That(sink.Facts[0].Second.Value, Is.EqualTo(42));
                Assert.That(sink.Facts[0].Kind, Is.EqualTo(PhysicsFactKind.TriggerEnter).Or.EqualTo(PhysicsFactKind.TriggerStay));
                Assert.That(pool.TryGetInstance(oldHandle, out stale), Is.False);
            }
            finally
            {
                foreignOwner.Dispose();
                if (foreignInstance != null) UnityEngine.Object.Destroy(foreignInstance);
                if (unboundInstance != null) UnityEngine.Object.Destroy(unboundInstance);
                participant.Dispose();
                UnityEngine.Object.Destroy(prefab);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FactOverflowFaultsAtTheControlledBoundary()
        {
            GameObject prefab = CreateSensor();
            UnityActorPool pool = new UnityActorPool();
            pool.RegisterPrefab(0, prefab, 3);
            pool.Seal();
            PoseSource source = new PoseSource { Poses = new[] { Pose(1, 0), Pose(2, .1f), Pose(3, .2f) } };
            FactSink sink = new FactSink();
            LocalPhysicsParticipant participant = new LocalPhysicsParticipant(pool, source, sink, maxFactsPerTick: 1);
            try
            {
                Assert.Throws<InvalidOperationException>(() => participant.Simulate(Context(1)));
                Assert.That(participant.Failure, Is.Not.Null);
                Assert.That(sink.Tick, Is.Zero, "Do not publish a partial fact batch.");
                Assert.Throws<InvalidOperationException>(() => participant.Simulate(Context(2)));
            }
            finally
            {
                participant.Dispose();
                UnityEngine.Object.Destroy(prefab);
            }
            yield return null;
        }

        private static GameObject CreateSensor()
        {
            GameObject prefab = new GameObject("Kinematic sensor template");
            prefab.SetActive(false);
            Rigidbody body = prefab.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            GameObject child = new GameObject("Second sensor collider");
            child.transform.SetParent(prefab.transform, false);
            child.AddComponent<BoxCollider>().isTrigger = true;
            return prefab;
        }
        private static string DescribeFacts(IReadOnlyList<PhysicsFact> facts)
        {
            List<string> descriptions = new List<string>();
            foreach (PhysicsFact fact in facts) descriptions.Add(fact.First.Value + "-" + fact.Second.Value + ":" + fact.Kind);
            return string.Join(", ", descriptions);
        }
        private static ActorPose Pose(ulong id, float x)
            => new ActorPose(new SimulationObjectId(id), 0, new Vector3(x, 0, 0), Quaternion.identity);
        private static SimulationContext Context(ulong tick)
            => new SimulationContext(new SimulationTick(tick, .02f), SimulationPhase.Physics);
        private sealed class PoseSource : IActorPoseSource
        {
            internal ActorPose[] Poses;
            public IReadOnlyList<ActorPose> ReadPoses() => Poses;
        }
        private sealed class FactSink : IPhysicsFactSink
        {
            internal ulong Tick;
            internal int Calls;
            internal IReadOnlyList<PhysicsFact> Facts = Array.Empty<PhysicsFact>();
            public void PublishPhysicsFacts(ulong tick, IReadOnlyList<PhysicsFact> facts)
            { Tick = tick; Facts = facts; Calls++; }
        }
    }
}
