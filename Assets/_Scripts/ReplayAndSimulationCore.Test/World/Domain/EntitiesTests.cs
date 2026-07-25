using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.World.Domain;

namespace ReplayAndSimulationCore.Test.World.Domain
{
    public sealed class EntitiesTests
    {
        [Test]
        public void Constructor_WhenCapacityIsZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Entities(0));
        }

        [Test]
        public void Create_WhenCapacityAvailable_AllocatesLowestSlotsWithMonotonicSequence()
        {
            Entities entities = new(3);

            Entity first = entities.Create();
            Entity second = entities.Create();
            Entity third = entities.Create();

            Assert.AreEqual(3, entities.Capacity);
            Assert.AreEqual(3, entities.AliveEntityCount);

            Assert.AreEqual(0, first.SlotId);
            Assert.AreEqual(1ul, first.SequenceId);
            Assert.AreEqual(EntityState.Alive, first.State);

            Assert.AreEqual(1, second.SlotId);
            Assert.AreEqual(2ul, second.SequenceId);

            Assert.AreEqual(2, third.SlotId);
            Assert.AreEqual(3ul, third.SequenceId);
        }

        [Test]
        public void Create_WhenCapacityIsExhausted_Throws()
        {
            Entities entities = new(1);
            entities.Create();

            Assert.Throws<InvalidOperationException>(() => entities.Create());
        }

        [Test]
        public void GetAliveEntityBySpawnSequence_WhenEntitiesCreated_ReturnsSpawnOrder()
        {
            Entities entities = new(3);
            Entity first = entities.Create();
            Entity second = entities.Create();
            Entity third = entities.Create();

            Assert.AreEqual(first, entities.GetAliveEntityBySpawnSequence(0));
            Assert.AreEqual(second, entities.GetAliveEntityBySpawnSequence(1));
            Assert.AreEqual(third, entities.GetAliveEntityBySpawnSequence(2));
        }

        [Test]
        public void IsAlive_WhenSlotOrSequenceDoesNotMatch_ReturnsFalse()
        {
            Entities entities = new(1);
            Entity entity = entities.Create();

            Assert.IsTrue(entities.IsAlive(entity.SlotId, entity.SequenceId));
            Assert.IsFalse(entities.IsAlive(entity.SlotId, entity.SequenceId + 1));
            Assert.IsFalse(entities.IsAlive(-1, entity.SequenceId));
            Assert.IsFalse(entities.IsAlive(entities.Capacity, entity.SequenceId));
        }

        [Test]
        public void MarkForDestroy_WhenEntityIsAlive_RemovesItFromAliveSequence()
        {
            Entities entities = new(3);
            Entity first = entities.Create();
            Entity second = entities.Create();
            Entity third = entities.Create();

            entities.MarkForDestroy(second.SlotId, second.SequenceId);

            Assert.AreEqual(2, entities.AliveEntityCount);
            Assert.IsFalse(entities.IsAlive(second.SlotId, second.SequenceId));

            Entity pendingDestroy = entities.GetEntity(second.SlotId, second.SequenceId);
            Assert.AreEqual(EntityState.Destroyed, pendingDestroy.State);

            Assert.AreEqual(first, entities.GetAliveEntityBySpawnSequence(0));
            Assert.AreEqual(third, entities.GetAliveEntityBySpawnSequence(1));
        }

        [Test]
        public void CommitDestroy_WhenEntityIsMarkedForDestroy_FreesSlotForReuseWithNewSequence()
        {
            Entities entities = new(2);
            Entity destroyed = entities.Create();
            Entity other = entities.Create();

            entities.MarkForDestroy(destroyed.SlotId, destroyed.SequenceId);
            entities.CommitDestroy(destroyed.SlotId, destroyed.SequenceId);

            Assert.IsFalse(entities.IsAlive(destroyed.SlotId, destroyed.SequenceId));

            Entity replacement = entities.Create();

            Assert.AreEqual(destroyed.SlotId, replacement.SlotId);
            Assert.AreEqual(3ul, replacement.SequenceId);
            Assert.AreEqual(EntityState.Alive, replacement.State);

            Assert.AreEqual(other, entities.GetAliveEntityBySpawnSequence(0));
            Assert.AreEqual(replacement, entities.GetAliveEntityBySpawnSequence(1));
        }

        [Test]
        public void CommitDestroy_WhenEntityWasNotMarkedForDestroy_Throws()
        {
            Entities entities = new(1);
            Entity entity = entities.Create();

            Assert.Throws<InvalidOperationException>(
                () => entities.CommitDestroy(entity.SlotId, entity.SequenceId));
        }

        [Test]
        public void Create_ReplayingSameScenario_ProducesSameEntitySnapshot()
        {
            List<string> expected = RunCreateScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunCreateScenario());
            }
        }

        [Test]
        public void CreateAndDestroy_ReplayingSameScenario_ProducesSameEntitySnapshot()
        {
            List<string> expected = RunCreateDestroyScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunCreateDestroyScenario());
            }
        }

        private static List<string> RunCreateScenario()
        {
            Entities entities = new(4);
            entities.Create();
            entities.Create();
            entities.Create();

            List<string> snapshot = new()
            {
                $"capacity:{entities.Capacity}",
                $"alive:{entities.AliveEntityCount}"
            };

            for (int i = 0; i < entities.AliveEntityCount; i++)
            {
                Entity entity = entities.GetAliveEntityBySpawnSequence(i);
                snapshot.Add($"{entity.SlotId}:{entity.SequenceId}:{entity.State}:{entities.IsAlive(entity.SlotId, entity.SequenceId)}");
            }

            snapshot.Add($"invalid:{entities.IsAlive(-1, 1)}:{entities.IsAlive(99, 1)}");
            return snapshot;
        }

        private static List<string> RunCreateDestroyScenario()
        {
            Entities entities = new(3);
            Entity first = entities.Create();
            Entity second = entities.Create();
            Entity third = entities.Create();

            entities.MarkForDestroy(second.SlotId, second.SequenceId);
            Entity pendingSecond = entities.GetEntity(second.SlotId, second.SequenceId);

            entities.CommitDestroy(second.SlotId, second.SequenceId);
            Entity secondSlotReplacement = entities.Create();

            entities.MarkForDestroy(first.SlotId, first.SequenceId);
            entities.CommitDestroy(first.SlotId, first.SequenceId);
            Entity firstSlotReplacement = entities.Create();

            List<string> snapshot = new()
            {
                $"pending:{pendingSecond.SlotId}:{pendingSecond.SequenceId}:{pendingSecond.State}",
                $"oldAlive:{entities.IsAlive(first.SlotId, first.SequenceId)}:{entities.IsAlive(second.SlotId, second.SequenceId)}",
                $"replacement:{secondSlotReplacement.SlotId}:{secondSlotReplacement.SequenceId}",
                $"replacement:{firstSlotReplacement.SlotId}:{firstSlotReplacement.SequenceId}",
                $"alive:{entities.AliveEntityCount}"
            };

            for (int i = 0; i < entities.AliveEntityCount; i++)
            {
                Entity entity = entities.GetAliveEntityBySpawnSequence(i);
                snapshot.Add($"{entity.SlotId}:{entity.SequenceId}:{entity.State}");
            }

            snapshot.Add($"thirdAlive:{entities.IsAlive(third.SlotId, third.SequenceId)}");
            return snapshot;
        }
    }
}
