using ECSManagement.Contract;
using ECSManagement.Domain;
using NUnit.Framework;

namespace ECSManagement.Test.Domain
{
    public sealed partial class EntityRegistryTests
    {
        [Test]
        public void Reserve_CalledRepeatedly_AllocatesIdsAndSpawnSequencesInStableOrder()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(3);

            // Act
            EntityHandle entity1 = registry.Reserve();
            EntityHandle entity2 = registry.Reserve();
            EntityHandle entity3 = registry.Reserve();

            // Assert
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                new[] { entity1.Id, entity2.Id, entity3.Id });
            CollectionAssert.AreEqual(
                new[] { 1ul, 2ul, 3ul },
                new[] { entity1.SpawnSequence, entity2.SpawnSequence, entity3.SpawnSequence });
        }

        [Test]
        public void GetAliveEntitiesBySpawnSequence_DifferentCommitOrder_ReturnsReserveOrder()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(10);
            EntityHandle entity1 = registry.Reserve();
            EntityHandle entity2 = registry.Reserve();
            EntityHandle entity3 = registry.Reserve();

            // Act
            registry.CommitCreate(entity2);
            registry.CommitCreate(entity1);
            registry.CommitCreate(entity3);

            // Assert
            EntityHandle[] aliveEntities = CopyAliveEntitiesBySpawnSequence(registry);
            CollectionAssert.AreEqual(
                new[] { entity1, entity2, entity3 },
                aliveEntities);
        }

        [Test]
        public void GetAliveEntitiesBySpawnSequence_AfterDestroy_ExcludesDestroyedEntityAndPreservesOrder()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(10);
            EntityHandle entity1 = registry.Reserve();
            EntityHandle entity2 = registry.Reserve();
            EntityHandle entity3 = registry.Reserve();
            registry.CommitCreate(entity1);
            registry.CommitCreate(entity2);
            registry.CommitCreate(entity3);

            // Act
            registry.MarkDestroy(entity1);
            registry.CommitDestroy(entity1);

            // Assert
            EntityHandle[] aliveEntities = CopyAliveEntitiesBySpawnSequence(registry);
            CollectionAssert.AreEqual(
                new[] { entity2, entity3 },
                aliveEntities);
        }

        [Test]
        public void GetAliveEntitiesBySpawnSequence_AfterSlotReuse_UsesSpawnSequenceOrder()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(3);
            EntityHandle entity1 = registry.Reserve();
            EntityHandle entity2 = registry.Reserve();
            EntityHandle entity3 = registry.Reserve();
            registry.CommitCreate(entity1);
            registry.CommitCreate(entity2);
            registry.CommitCreate(entity3);

            // Act
            registry.MarkDestroy(entity1);
            registry.CommitDestroy(entity1);
            EntityHandle entity4 = registry.Reserve();
            registry.CommitCreate(entity4);

            // Assert
            EntityHandle[] aliveEntities = CopyAliveEntitiesBySpawnSequence(registry);
            CollectionAssert.AreEqual(
                new[] { entity2, entity3, entity4 },
                aliveEntities);
        }

        [Test]
        public void SameOperationSequence_OnDifferentRegistries_ProducesSameAliveSequence()
        {
            // Act
            EntityHandle[] firstRun = RunDeterministicEntityScenario();
            EntityHandle[] secondRun = RunDeterministicEntityScenario();

            // Assert
            CollectionAssert.AreEqual(firstRun, secondRun);
        }

        private static EntityHandle[] RunDeterministicEntityScenario()
        {
            EntityRegistry registry = new EntityRegistry(4);
            EntityHandle entity1 = registry.Reserve();
            EntityHandle entity2 = registry.Reserve();
            EntityHandle entity3 = registry.Reserve();
            registry.CommitCreate(entity2);
            registry.CommitCreate(entity1);
            registry.CommitCreate(entity3);

            registry.MarkDestroy(entity2);
            registry.CommitDestroy(entity2);

            EntityHandle entity4 = registry.Reserve();
            registry.CommitCreate(entity4);

            return CopyAliveEntitiesBySpawnSequence(registry);
        }

        private static EntityHandle[] CopyAliveEntitiesBySpawnSequence(
            EntityRegistry registry)
        {
            EntityHandle[] entities = new EntityHandle[registry.AliveEntityCount];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = registry.GetAliveEntityBySpawnSequenceIndex(i);
            }

            return entities;
        }
    }
}
