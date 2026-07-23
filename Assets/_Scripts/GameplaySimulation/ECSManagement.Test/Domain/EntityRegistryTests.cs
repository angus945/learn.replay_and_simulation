using System;
using ECSManagement.Contract;
using ECSManagement.Domain;
using NUnit.Framework;

namespace ECSManagement.Test.Domain
{
    [TestFixture]
    public sealed partial class EntityRegistryTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_InvalidCapacity_Throws(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EntityRegistry(capacity));
        }

        [Test]
        public void Constructor_ValidCapacity_StoresCapacity()
        {
            // Arrange
            const int capacity = 10;

            // Act
            EntityRegistry registry = new EntityRegistry(capacity);

            // Assert
            Assert.AreEqual(capacity, registry.Capacity);
        }

        [Test]
        public void Reserve_WhenSlotAvailable_ReturnsReservedEntityThatCanBuildButIsNotAlive()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);

            // Act
            EntityHandle entity = registry.Reserve();

            // Assert
            Assert.AreEqual(0, entity.Id);
            Assert.AreEqual(1u, entity.Generation);
            Assert.AreEqual(1ul, entity.SpawnSequence);
            Assert.IsTrue(registry.CanBuild(entity));
            Assert.IsFalse(registry.IsAlive(entity));
        }

        [Test]
        public void Reserve_WhenCapacityExhausted_Throws()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            registry.Reserve();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => registry.Reserve());
        }

        [Test]
        public void CommitCreate_ReservedEntity_BecomesAlive()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle entity = registry.Reserve();

            // Act
            registry.CommitCreate(entity);

            // Assert
            Assert.IsTrue(registry.IsAlive(entity));
            Assert.IsTrue(registry.CanBuild(entity));
        }

        [Test]
        public void AbortCreate_ReservedEntity_ReleasesSlotAndInvalidatesHandle()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle abortedEntity = registry.Reserve();

            // Act
            registry.AbortCreate(abortedEntity);
            EntityHandle nextEntity = registry.Reserve();

            // Assert
            Assert.IsFalse(registry.IsAlive(abortedEntity));
            Assert.IsFalse(registry.CanBuild(abortedEntity));
            Assert.AreEqual(abortedEntity.Id, nextEntity.Id);
            Assert.AreNotEqual(abortedEntity.Generation, nextEntity.Generation);
            Assert.AreNotEqual(abortedEntity.SpawnSequence, nextEntity.SpawnSequence);
        }

        [Test]
        public void MarkDestroy_AliveEntity_RemovesEntityFromAliveQueries()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle entity = registry.Reserve();
            registry.CommitCreate(entity);

            // Act
            registry.MarkDestroy(entity);

            // Assert
            Assert.IsFalse(registry.IsAlive(entity));
            Assert.IsFalse(registry.CanBuild(entity));
            Assert.IsFalse(registry.TryGetAliveEntity(entity.Id, out _));
            Assert.IsFalse(registry.TryGetAliveEntityBySpawnSequence(
                entity.SpawnSequence,
                out _));
        }

        [Test]
        public void CommitDestroy_PendingDestroyEntity_ReleasesSlotForReuse()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle destroyedEntity = registry.Reserve();
            registry.CommitCreate(destroyedEntity);
            registry.MarkDestroy(destroyedEntity);

            // Act
            registry.CommitDestroy(destroyedEntity);
            EntityHandle nextEntity = registry.Reserve();

            // Assert
            Assert.AreEqual(destroyedEntity.Id, nextEntity.Id);
            Assert.AreNotEqual(destroyedEntity.Generation, nextEntity.Generation);
            Assert.AreNotEqual(destroyedEntity.SpawnSequence, nextEntity.SpawnSequence);
        }

        [Test]
        public void TryGetAliveEntity_AliveEntity_ReturnsSameHandle()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle entity = registry.Reserve();
            registry.CommitCreate(entity);

            // Act
            bool found = registry.TryGetAliveEntity(entity.Id, out EntityHandle foundEntity);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(entity, foundEntity);
        }

        [Test]
        public void TryGetAliveEntity_InvalidId_ReturnsFalse()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);

            // Act / Assert
            Assert.IsFalse(registry.TryGetAliveEntity(-1, out _));
            Assert.IsFalse(registry.TryGetAliveEntity(1, out _));
        }

        [Test]
        public void TryGetAliveEntityBySpawnSequence_AliveEntity_ReturnsSameHandle()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle entity = registry.Reserve();
            registry.CommitCreate(entity);

            // Act
            bool found = registry.TryGetAliveEntityBySpawnSequence(
                entity.SpawnSequence,
                out EntityHandle foundEntity);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(entity, foundEntity);
        }

        [Test]
        public void TryGetAliveEntityBySpawnSequence_UnknownSpawnSequence_ReturnsFalse()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);

            // Act / Assert
            Assert.IsFalse(registry.TryGetAliveEntityBySpawnSequence(1, out _));
        }

        [Test]
        public void StaleHandle_AfterSlotReuse_IsRejected()
        {
            // Arrange
            EntityRegistry registry = new EntityRegistry(1);
            EntityHandle oldEntity = registry.Reserve();
            registry.CommitCreate(oldEntity);
            registry.MarkDestroy(oldEntity);
            registry.CommitDestroy(oldEntity);

            EntityHandle newEntity = registry.Reserve();
            registry.CommitCreate(newEntity);

            // Act / Assert
            Assert.IsFalse(registry.IsAlive(oldEntity));
            Assert.IsFalse(registry.CanBuild(oldEntity));
            Assert.Throws<InvalidOperationException>(
                () => registry.MarkDestroy(oldEntity));
        }
    }
}
