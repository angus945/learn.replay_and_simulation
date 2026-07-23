using System;
using ECSManagement.API;
using ECSManagement.Application;
using ECSManagement.Contract;
using ECSManagement.Test.Fixtures;
using NUnit.Framework;

namespace ECSManagement.Test.Application
{
    [TestFixture]
    public sealed class EntityFilterTests
    {
        [Test]
        public void EntityCount_BeforeBuild_Throws()
        {
            // Arrange
            EntityFilter filter = (EntityFilter)CreateWorld().CreateFilter();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => _ = filter.EntityCount);
        }

        [Test]
        public void GetEntity_BeforeBuild_Throws()
        {
            // Arrange
            EntityFilter filter = (EntityFilter)CreateWorld().CreateFilter();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => filter.GetEntity(0));
        }

        [Test]
        public void Contains_BeforeBuild_Throws()
        {
            // Arrange
            EntityFilter filter = (EntityFilter)CreateWorld().CreateFilter();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => filter.Contains(default));
        }

        [Test]
        public void Build_CalledTwice_Throws()
        {
            // Arrange
            EntityFilter filter = (EntityFilter)CreateWorld().CreateFilter();
            filter.Build();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => filter.Build());
        }

        [Test]
        public void WithOrWithout_AfterBuild_Throws()
        {
            // Arrange
            EntityFilter filter = (EntityFilter)CreateWorld().CreateFilter();
            filter.Build();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => filter.With<TestComponent>());
            Assert.Throws<InvalidOperationException>(() => filter.Without<TestComponent>());
        }

        [Test]
        public void GetEntity_IndexOutOfRange_Throws()
        {
            // Arrange
            IEntityFilter filter = CreateWorld().CreateFilter()
                .With<TestComponent>()
                .Build();

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.GetEntity(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.GetEntity(0));
        }

        [Test]
        public void SpawnRequest_BeforeStructuralCommit_DoesNotParticipateInRuntimeQueries()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Build();

            // Act
            RequestTestEntity(world, 42);

            // Assert
            Assert.AreEqual(0, world.EntityCountBySpawnSequence);
            Assert.AreEqual(0, filter.EntityCount);

            world.CommitStructuralChanges();

            EntityHandle entity = world.GetEntityBySpawnSequenceIndex(0);
            Assert.IsTrue(world.HasComponent<TestComponent>(entity));
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(entity, filter.GetEntity(0));
        }

        [Test]
        public void With_RequiredComponent_IncludesOnlyMatchingEntities()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle matchingEntity = SpawnTestEntity(world, 1);
            EntityHandle nonMatchingEntity = SpawnEmptyEntity(world);

            // Act
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Build();

            // Assert
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(matchingEntity, filter.GetEntity(0));
            Assert.IsTrue(filter.Contains(matchingEntity));
            Assert.IsFalse(filter.Contains(nonMatchingEntity));
        }

        [Test]
        public void With_MultipleRequiredComponents_RequiresAllComponents()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle partialMatch = SpawnTestEntity(world, 1);
            EntityHandle fullMatch = SpawnTestEntity(world, 2);
            world.AddComponent(fullMatch, new SecondaryTestComponent(20));

            // Act
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .With<SecondaryTestComponent>()
                .Build();

            // Assert
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(fullMatch, filter.GetEntity(0));
            Assert.IsFalse(filter.Contains(partialMatch));
        }

        [Test]
        public void Without_ExcludedComponent_ExcludesEntitiesWithComponent()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle includedEntity = SpawnTestEntity(world, 1);
            EntityHandle excludedEntity = SpawnTestEntity(world, 2);
            world.AddComponent(excludedEntity, new ExcludedTestComponent());

            // Act
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Without<ExcludedTestComponent>()
                .Build();

            // Assert
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(includedEntity, filter.GetEntity(0));
            Assert.IsFalse(filter.Contains(excludedEntity));
        }

        [Test]
        public void Without_OnlyExcludedComponent_IncludesEntitiesWithoutExcludedComponent()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle includedEntity = SpawnEmptyEntity(world);
            EntityHandle excludedEntity = SpawnEmptyEntity(world);
            world.AddComponent(excludedEntity, new ExcludedTestComponent());

            // Act
            IEntityFilter filter = world.CreateFilter()
                .Without<ExcludedTestComponent>()
                .Build();

            // Assert
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(includedEntity, filter.GetEntity(0));
            Assert.IsTrue(filter.Contains(includedEntity));
            Assert.IsFalse(filter.Contains(excludedEntity));
        }

        [Test]
        public void DuplicateWithOrWithout_DoesNotChangeMatches()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle matchingEntity = SpawnTestEntity(world, 1);

            // Act
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .With<TestComponent>()
                .Without<ExcludedTestComponent>()
                .Without<ExcludedTestComponent>()
                .Build();

            // Assert
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(matchingEntity, filter.GetEntity(0));
        }

        [Test]
        public void BuiltFilter_UpdatesWhenMatchingEntitySpawns()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Build();

            // Act
            RequestTestEntity(world, 42);

            // Assert
            Assert.AreEqual(0, filter.EntityCount);

            world.CommitStructuralChanges();

            EntityHandle entity = world.GetEntityBySpawnSequenceIndex(0);
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(entity, filter.GetEntity(0));
        }

        [Test]
        public void BuiltFilter_UpdatesWhenComponentIsAddedAndRemoved()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle entity = SpawnTestEntity(world, 1);
            IEntityFilter filter = world.CreateFilter()
                .With<SecondaryTestComponent>()
                .Build();

            // Act / Assert
            Assert.AreEqual(0, filter.EntityCount);

            world.AddComponent(entity, new SecondaryTestComponent(10));
            Assert.AreEqual(1, filter.EntityCount);
            Assert.AreEqual(entity, filter.GetEntity(0));

            world.RemoveComponent<SecondaryTestComponent>(entity);
            Assert.AreEqual(0, filter.EntityCount);
        }

        [Test]
        public void BuiltFilter_UpdatesWhenEntityIsDestroyed()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle entity = SpawnTestEntity(world, 42);
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Build();

            // Act
            world.Destroy(entity);

            // Assert
            Assert.AreEqual(0, filter.EntityCount);
            Assert.IsFalse(filter.Contains(entity));
        }

        [Test]
        public void DestroyAndSpawnRequest_SameStructuralCommit_ReusesDestroyedSlotOnNextTick()
        {
            // Arrange
            EcsWorld world = CreateWorld(1);
            EntityHandle destroyedEntity = SpawnTestEntity(world, 42);

            // Act
            world.Destroy(destroyedEntity);
            RequestTestEntity(world, 99);

            // Assert
            Assert.AreEqual(0, world.EntityCountBySpawnSequence);

            world.CommitStructuralChanges();

            EntityHandle nextEntity = world.GetEntityBySpawnSequenceIndex(0);
            Assert.AreEqual(destroyedEntity.Id, nextEntity.Id);
            Assert.AreNotEqual(destroyedEntity.Generation, nextEntity.Generation);
            Assert.AreNotEqual(
                destroyedEntity.SpawnSequence,
                nextEntity.SpawnSequence);
            Assert.AreEqual(
                99,
                world.GetComponent<TestComponent>(nextEntity).Value);
        }

        [Test]
        public void BuiltFilter_AfterSlotReuse_ReturnsEntitiesInSpawnSequenceOrder()
        {
            // Arrange
            EcsWorld world = CreateWorld(3);
            EntityHandle entity1 = SpawnTestEntity(world, 1);
            EntityHandle entity2 = SpawnTestEntity(world, 2);
            EntityHandle entity3 = SpawnTestEntity(world, 3);
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Build();

            // Act
            world.Destroy(entity1);
            world.CommitStructuralChanges();
            EntityHandle entity4 = SpawnTestEntity(world, 4);

            // Assert
            CollectionAssert.AreEqual(
                new[] { entity2, entity3, entity4 },
                CopyFilterEntities(filter));
        }

        [Test]
        public void BuiltFilter_AfterComponentMutations_ReturnsEntitiesInSpawnSequenceOrder()
        {
            // Arrange
            EcsWorld world = CreateWorld();
            EntityHandle entity1 = SpawnTestEntity(world, 1);
            EntityHandle entity2 = SpawnTestEntity(world, 2);
            EntityHandle entity3 = SpawnTestEntity(world, 3);
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Without<ExcludedTestComponent>()
                .Build();

            // Act
            world.AddComponent(entity1, new ExcludedTestComponent());
            world.AddComponent(entity2, new ExcludedTestComponent());
            world.RemoveComponent<ExcludedTestComponent>(entity2);
            world.RemoveComponent<ExcludedTestComponent>(entity1);

            // Assert
            CollectionAssert.AreEqual(
                new[] { entity1, entity2, entity3 },
                CopyFilterEntities(filter));
        }

        [Test]
        public void SameOperationSequence_OnDifferentWorlds_ProducesSameFilterSnapshot()
        {
            // Act
            EntityHandle[] firstRun = RunDeterministicFilterScenario();
            EntityHandle[] secondRun = RunDeterministicFilterScenario();

            // Assert
            CollectionAssert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void DeterministicScenario_ReturnsMatchesInSpawnSequenceOrder()
        {
            // Act
            EntityHandle[] entities = RunDeterministicFilterScenario();

            // Assert
            CollectionAssert.AreEqual(
                new[]
                {
                    new EntityHandle(1, 1, 2),
                    new EntityHandle(2, 1, 3),
                    new EntityHandle(0, 2, 4),
                },
                entities);
        }

        private static EcsWorld CreateWorld(int entityCapacity = 10)
        {
            EcsWorld world = new EcsWorld(entityCapacity);
            world.RegisterComponent<TestComponent>();
            world.RegisterComponent<SecondaryTestComponent>();
            world.RegisterComponent<ExcludedTestComponent>();

            return world;
        }

        private static EntityHandle SpawnTestEntity(
            EcsWorld world,
            int componentValue)
        {
            RequestTestEntity(world, componentValue);
            world.CommitStructuralChanges();
            return world.GetEntityBySpawnSequenceIndex(
                world.EntityCountBySpawnSequence - 1);
        }

        private static void RequestTestEntity(
            EcsWorld world,
            int componentValue)
        {
            world.SpawnRequest(
                new TestEntityRecipe(),
                new TestSpawnArguments(componentValue));
        }

        private static EntityHandle SpawnEmptyEntity(EcsWorld world)
        {
            world.SpawnRequest(
                new EmptyEntityRecipe(),
                new EmptySpawnArguments());
            world.CommitStructuralChanges();
            return world.GetEntityBySpawnSequenceIndex(
                world.EntityCountBySpawnSequence - 1);
        }

        private static EntityHandle[] CopyFilterEntities(IEntityFilter filter)
        {
            EntityHandle[] entities = new EntityHandle[filter.EntityCount];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = filter.GetEntity(i);
            }

            return entities;
        }

        private static EntityHandle[] RunDeterministicFilterScenario()
        {
            EcsWorld world = CreateWorld(4);
            EntityHandle entity1 = SpawnTestEntity(world, 1);
            EntityHandle entity2 = SpawnTestEntity(world, 2);
            EntityHandle entity3 = SpawnTestEntity(world, 3);
            IEntityFilter filter = world.CreateFilter()
                .With<TestComponent>()
                .Without<ExcludedTestComponent>()
                .Build();

            world.AddComponent(entity2, new ExcludedTestComponent());
            world.Destroy(entity1);
            world.CommitStructuralChanges();
            EntityHandle entity4 = SpawnTestEntity(world, 4);
            world.RemoveComponent<ExcludedTestComponent>(entity2);
            world.AddComponent(entity3, new SecondaryTestComponent(30));

            return CopyFilterEntities(filter);
        }
    }
}
