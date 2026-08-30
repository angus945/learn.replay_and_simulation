using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationObjects.Contract;

namespace SimulationObjects.Tests
{
    public sealed class SimulationObjectRegistryTests
    {
        [Test]
        public void Spawn_IsReservedButNotActiveUntilCommit()
        {
            var registry = new SimulationObjectRegistry();
            var pending = registry.RequestSpawn();
            Assert.That(pending.State, Is.EqualTo(SimulationObjectState.PendingSpawn));
            Assert.That(pending.SpawnSequence, Is.Zero);
            Assert.That(registry.GetActiveOrdered(), Is.Empty);
            Assert.That(registry.TryGet(pending.Id, out _), Is.True);
            var changes = registry.Commit();
            Assert.That(changes.Spawned[0].Id, Is.EqualTo(pending.Id));
            Assert.That(changes.Spawned[0].State, Is.EqualTo(SimulationObjectState.Alive));
            Assert.That(registry.GetActiveOrdered().Count, Is.EqualTo(1));
        }

        [Test]
        public void Destroy_RemainsActiveUntilCommitThenInvalidatesIdAndHandle()
        {
            var registry = new SimulationObjectRegistry();
            var item = registry.RequestSpawn();
            registry.Commit();
            Assert.That(registry.RequestDestroy(item.Handle), Is.True);
            Assert.That(registry.RequestDestroy(item.Handle), Is.False);
            Assert.That(registry.GetActiveOrdered()[0].State, Is.EqualTo(SimulationObjectState.PendingDestroy));
            Assert.That(registry.Commit().Destroyed[0].Id, Is.EqualTo(item.Id));
            Assert.That(registry.TryGet(item.Id, out _), Is.False);
            Assert.That(registry.TryGet(item.Handle, out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.RequestDestroy(item.Handle));
        }

        [Test]
        public void Reuse_ChoosesLowestSlotButNeverReusesIdentity()
        {
            var registry = new SimulationObjectRegistry();
            var first = registry.RequestSpawn();
            var second = registry.RequestSpawn();
            registry.Commit();
            registry.RequestDestroy(second.Handle);
            registry.RequestDestroy(first.Handle);
            registry.Commit();
            var replacement = registry.RequestSpawn();
            Assert.That(replacement.Handle.Slot, Is.EqualTo(first.Handle.Slot));
            Assert.That(replacement.Handle.Generation, Is.EqualTo(first.Handle.Generation + 1));
            Assert.That(replacement.Id.Value, Is.GreaterThan(second.Id.Value));
            Assert.That(registry.TryGet(first.Handle, out _), Is.False);
        }

        [Test]
        public void CancelledSpawn_NeverBecomesAliveAndDoesNotConsumeSpawnSequence()
        {
            var registry = new SimulationObjectRegistry();
            var cancelled = registry.RequestSpawn();
            registry.RequestDestroy(cancelled.Handle);
            var kept = registry.RequestSpawn();
            var changes = registry.Commit();
            Assert.That(changes.CancelledSpawns[0].Id, Is.EqualTo(cancelled.Id));
            Assert.That(changes.Destroyed, Is.Empty);
            Assert.That(changes.Spawned[0].Id, Is.EqualTo(kept.Id));
            Assert.That(changes.Spawned[0].SpawnSequence, Is.EqualTo(1));
            Assert.That(registry.TryGet(cancelled.Handle, out _), Is.False);
        }

        [Test]
        public void StableOrder_IsNotSlotOrderOrDestroyRequestOrder()
        {
            var registry = new SimulationObjectRegistry();
            var first = registry.RequestSpawn();
            var second = registry.RequestSpawn();
            registry.Commit();
            registry.RequestDestroy(first.Handle);
            registry.Commit();
            var third = registry.RequestSpawn();
            registry.Commit();
            var active = registry.GetActiveOrdered();
            Assert.That(third.Handle.Slot, Is.LessThan(second.Handle.Slot));
            Assert.That(active[0].Id, Is.EqualTo(second.Id));
            Assert.That(active[1].Id, Is.EqualTo(third.Id));
            registry.RequestDestroy(third.Handle);
            registry.RequestDestroy(second.Handle);
            var removed = registry.Commit().Destroyed;
            Assert.That(removed[0].Id, Is.EqualTo(second.Id));
        }

        [Test]
        public void Capacity_IsNotFreedUntilCommit()
        {
            var registry = new SimulationObjectRegistry(1);
            var first = registry.RequestSpawn();
            registry.RequestDestroy(first.Handle);
            Assert.Throws<InvalidOperationException>(() => registry.RequestSpawn());
            registry.Commit();
            Assert.That(registry.RequestSpawn().Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void QueriesAndCommitResults_AreStableReadOnlyCopies()
        {
            var registry = new SimulationObjectRegistry();
            var first = registry.RequestSpawn();
            var before = registry.GetObjectsOrdered();
            var changes = registry.Commit();
            registry.RequestDestroy(first.Handle);
            registry.Commit();
            Assert.That(before[0].State, Is.EqualTo(SimulationObjectState.PendingSpawn));
            Assert.That(changes.Spawned[0].State, Is.EqualTo(SimulationObjectState.Alive));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<SimulationObjectRecord>)before)[0] = default);
        }

        [Test]
        public void EmptyCommitAndInvalidIdentities_AreWellDefined()
        {
            var registry = new SimulationObjectRegistry();
            Assert.That(registry.Commit().Spawned, Is.Empty);
            Assert.That(registry.Commit().Destroyed, Is.Empty);
            Assert.That(registry.TryGet(default(SimulationObjectId), out _), Is.False);
            Assert.That(registry.TryGet(default(SimulationObjectHandle), out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationObjectId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationObjectHandle(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationObjectHandle(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationObjectRegistry(0));
        }

        [Test]
        public void SameOperations_ProduceSameIdentityAndOrderingTrace()
        {
            CollectionAssert.AreEqual(Trace(), Trace());
        }

        private static List<string> Trace()
        {
            var registry = new SimulationObjectRegistry();
            var trace = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var pending = registry.RequestSpawn();
                registry.Commit();
                foreach (var entry in registry.GetActiveOrdered())
                    trace.Add($"{entry.Id}:{entry.Handle}:{entry.SpawnSequence}:{entry.State}");
                registry.RequestDestroy(pending.Handle);
                registry.Commit();
            }
            return trace;
        }
    }
}
