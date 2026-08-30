using System;
using System.Collections.Generic;
using DeterministicSimulation.Framework;
using NUnit.Framework;
using SimulationObjects.Contract;
using UnityEngine;

namespace DeterministicSimulation.Unity.Tests
{
    public sealed class ActorAdapterTests
    {
        [Test]
        public void PhasePrecedenceIsIndependentOfCallbackOrder() => PhysicsFactContractChecks.PhasePrecedenceIsIndependentOfCallbackOrder();

        [Test]
        public void CapacityCountsNormalizedContactsAndSnapshotsAreDetached() => PhysicsFactContractChecks.CapacityCountsNormalizedContactsAndSnapshotsAreDetached();

        private GameObject prefab;
        private UnityActorPool pool;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("Actor template");
            prefab.SetActive(false);
            pool = new UnityActorPool();
        }
        [TearDown]
        public void TearDown()
        {
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void AllocationAndReuseKeepObjectIdsSeparateFromInstanceGeneration()
        {
            pool.RegisterPrefab(9, prefab, 1);
            pool.RegisterPrefab(3, prefab, 1);
            pool.Seal();
            pool.Reconcile(new[] { Pose(20, 9, 2), Pose(10, 3, 1) });
            IReadOnlyList<ActorBinding> before = pool.GetActiveBindings();
            Assert.That(before[0].Id.Value, Is.EqualTo(10));
            Assert.That(before[0].Instance.Slot, Is.EqualTo(0));
            Assert.That(before[1].Instance.Slot, Is.EqualTo(1));
            InstanceHandle old = before[1].Instance;
            Assert.That(pool.TryGetInstance(old, out GameObject original), Is.True);

            pool.Reconcile(new[] { Pose(30, 9, 30), Pose(10, 3, 1) });
            ActorBinding replacement = pool.GetActiveBindings()[1];
            Assert.That(replacement.Id.Value, Is.EqualTo(30));
            Assert.That(replacement.Instance.Slot, Is.EqualTo(old.Slot));
            Assert.That(replacement.Instance.Generation, Is.EqualTo(old.Generation + 1));
            Assert.That(pool.TryGetInstance(old, out GameObject stale), Is.False);
            Assert.That(pool.TryGetInstance(replacement.Instance, out GameObject reused), Is.True);
            Assert.That(reused, Is.SameAs(original));
            Assert.That(reused.GetComponent<UnityActorBinding>().ObjectId.Value, Is.EqualTo(30));
            Assert.That(reused.transform.position.x, Is.EqualTo(30));
            Assert.That(before[1].Id.Value, Is.EqualTo(20), "Returned bindings must be detached snapshots.");
        }

        [Test]
        public void InvalidSnapshotIsRejectedBeforeRemovingExistingBindings()
        {
            pool.RegisterPrefab(0, prefab, 1);
            pool.Seal();
            pool.Reconcile(new[] { Pose(5, 0, 4) });
            InstanceHandle initial = pool.GetActiveBindings()[0].Instance;
            Assert.Throws<InvalidOperationException>(() => pool.Reconcile(new[] { Pose(6, 0, 6), Pose(7, 0, 7) }));
            Assert.Throws<ArgumentException>(() => pool.Reconcile(new[] { Pose(6, 0, 6), Pose(6, 0, 7) }));
            Assert.Throws<ArgumentException>(() => pool.Reconcile(new[] { default(ActorPose) }));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.TryGetInstance(initial, out GameObject instance), Is.True);
            Assert.That(instance.transform.position.x, Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(() => pool.RegisterPrefab(1, prefab, 1));
        }

        [Test]
        public void PresentationInterpolatesSnapshotsAndSnapsNewOrDiscontinuousObjects()
        {
            pool.RegisterPrefab(0, prefab, 2);
            pool.Seal();
            PoseSource source = new PoseSource { Poses = new[] { Pose(1, 0, 10) } };
            UnityActorPresentation presentation = new UnityActorPresentation(pool, source);
            presentation.CaptureTickState(Context(1));
            presentation.Render(Context(1), 0);
            Assert.That(Position(0), Is.EqualTo(10));

            source.Poses = new[] { new ActorPose(new SimulationObjectId(1), 0, new Vector3(20, 0, 0), Quaternion.Euler(0, 90, 0)) };
            presentation.CaptureTickState(Context(2));
            source.Poses[0] = Pose(1, 0, 999);
            presentation.Render(Context(2), .5f);
            Assert.That(Position(0), Is.EqualTo(15));
            pool.TryGetInstance(pool.GetActiveBindings()[0].Instance, out GameObject rotating);
            Assert.That(Quaternion.Angle(rotating.transform.rotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(.001f));

            source.Poses = new[] { Pose(1, 0, 30), Pose(2, 0, 50) };
            presentation.CaptureTickState(Context(3));
            presentation.Render(Context(3), 0);
            Assert.That(Position(1), Is.EqualTo(50), "New objects must not interpolate from the origin.");
            InstanceHandle removed = pool.GetActiveBindings()[0].Instance;
            source.Poses = new[] { Pose(2, 0, 100) };
            presentation.CaptureTickState(Context(100));
            presentation.Render(Context(100), 0);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.TryGetInstance(removed, out GameObject stale), Is.False);
            Assert.That(Position(0), Is.EqualTo(100), "A tick gap must not interpolate unrelated snapshots.");
        }

        [Test]
        public void ExplicitSnapDoesNotInterpolateAcrossSessionsWithAdjacentTicks()
        {
            pool.RegisterPrefab(0, prefab, 1);
            pool.Seal();
            PoseSource source = new PoseSource { Poses = new[] { Pose(1, 0, 10) } };
            UnityActorPresentation presentation = new UnityActorPresentation(pool, source);
            presentation.CaptureTickState(Context(50));
            source.Poses = new[] { Pose(1, 0, 20) };
            presentation.CaptureTickState(Context(51));
            presentation.Render(Context(51), .5f);
            Assert.That(Position(0), Is.EqualTo(15));

            // Replay and live sessions may reuse the same ID and have adjacent tick numbers.
            source.Poses = new[] { Pose(1, 0, 500) };
            presentation.SnapToCurrent(Context(52));
            presentation.Render(Context(52), 0);
            Assert.That(Position(0), Is.EqualTo(500));
            source.Poses = new[] { Pose(1, 0, 510) };
            presentation.CaptureTickState(Context(53));
            presentation.Render(Context(53), .5f);
            Assert.That(Position(0), Is.EqualTo(505), "Normal interpolation must resume after an explicit snap.");
        }

        [Test]
        public void PhysicsFactsCanonicalizeMirroredPairsAndSortByIdentityThenKind()
        {
            SortedSet<PhysicsFact> facts = new SortedSet<PhysicsFact>
            {
                new PhysicsFact(new SimulationObjectId(9), new SimulationObjectId(2), PhysicsFactKind.TriggerEnter),
                new PhysicsFact(new SimulationObjectId(2), new SimulationObjectId(9), PhysicsFactKind.TriggerEnter),
                new PhysicsFact(new SimulationObjectId(1), new SimulationObjectId(3), PhysicsFactKind.TriggerExit)
            };
            Assert.That(facts.Count, Is.EqualTo(2));
            List<PhysicsFact> ordered = new List<PhysicsFact>(facts);
            Assert.That(ordered[0].First.Value, Is.EqualTo(1));
            Assert.That(ordered[1].First.Value, Is.EqualTo(2));
            Assert.That(ordered[1].Second.Value, Is.EqualTo(9));
        }

        private float Position(int bindingIndex)
        {
            pool.TryGetInstance(pool.GetActiveBindings()[bindingIndex].Instance, out GameObject instance);
            return instance.transform.position.x;
        }
        private static ActorPose Pose(ulong id, int archetype, float x)
            => new ActorPose(new SimulationObjectId(id), archetype, new Vector3(x, 0, 0), Quaternion.identity);
        private static SimulationContext Context(ulong tick)
            => new SimulationContext(new SimulationTick(tick, .02f), SimulationPhase.PresentationCapture);
        private sealed class PoseSource : IActorPoseSource
        {
            internal ActorPose[] Poses;
            public IReadOnlyList<ActorPose> ReadPoses() => Poses;
        }
    }
}
