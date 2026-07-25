using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace ReplayAndSimulationCore.Test.World.Application
{
    public sealed class EntityFilterTests
    {
        [Test]
        public void Build_WhenFilterIsCreated_RegistersItInEntityFilters()
        {
            EntityFilters filters = new(new Entities(4), CreateStores());

            IEntityFilter filter = filters.CreateFilter()
                .With<PositionComponent>()
                .Without<BlockerComponent>()
                .Build();

            Assert.IsNotNull(filter);
            Assert.AreEqual(1, filters.Count);
        }

        [Test]
        public void RebuildMatches_WhenRequiredAndExcludedComponentsAreConfigured_FiltersCommittedEntities()
        {
            Entities entities = new(4);
            ComponentStores stores = CreateStores();
            Entity first = CreateEntity(entities, stores, 10, hasMarker: true, hasBlocker: false);
            CreateEntity(entities, stores, 20, hasMarker: false, hasBlocker: false);
            CreateEntity(entities, stores, 30, hasMarker: true, hasBlocker: true);
            Entity fourth = CreateEntity(entities, stores, 40, hasMarker: true, hasBlocker: false);
            EntityFilter filter = new(
                entities,
                stores,
                new[] { typeof(PositionComponent), typeof(MarkerComponent) },
                new[] { typeof(BlockerComponent) });

            filter.RebuildMatches();

            Assert.AreEqual(2, filter.EntityCount);
            Assert.AreEqual($"{first.SlotId}:{first.SequenceId}", filter.GetEntity(0).ToString());
            Assert.AreEqual($"{fourth.SlotId}:{fourth.SequenceId}", filter.GetEntity(1).ToString());
        }

        [Test]
        public void Contains_WhenEntityMatchesCurrentComponents_ReturnsExpectedResultWithoutRebuild()
        {
            Entities entities = new(3);
            ComponentStores stores = CreateStores();
            Entity matching = CreateEntity(entities, stores, 10, hasMarker: true, hasBlocker: false);
            Entity missingMarker = CreateEntity(entities, stores, 20, hasMarker: false, hasBlocker: false);
            Entity blocked = CreateEntity(entities, stores, 30, hasMarker: true, hasBlocker: true);
            EntityFilter filter = new(
                entities,
                stores,
                new[] { typeof(PositionComponent), typeof(MarkerComponent) },
                new[] { typeof(BlockerComponent) });

            Assert.IsTrue(filter.Contains(ToHandle(matching)));
            Assert.IsFalse(filter.Contains(ToHandle(missingMarker)));
            Assert.IsFalse(filter.Contains(ToHandle(blocked)));
        }

        [Test]
        public void GetEntity_WhenIndexIsOutOfRange_Throws()
        {
            EntityFilter filter = new(
                new Entities(1),
                CreateStores(),
                new[] { typeof(PositionComponent) },
                Array.Empty<Type>());

            Assert.Throws<ArgumentOutOfRangeException>(() => filter.GetEntity(0));
        }

        [Test]
        public void RebuildMatches_ReplayingSameScenario_ProducesSameFilterSnapshot()
        {
            List<string> expected = RunFilterScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunFilterScenario());
            }
        }

        private static List<string> RunFilterScenario()
        {
            Entities entities = new(5);
            ComponentStores stores = CreateStores();
            CreateEntity(entities, stores, 1, hasMarker: true, hasBlocker: false);
            CreateEntity(entities, stores, 2, hasMarker: true, hasBlocker: true);
            CreateEntity(entities, stores, 3, hasMarker: false, hasBlocker: false);
            CreateEntity(entities, stores, 4, hasMarker: true, hasBlocker: false);
            EntityFilter filter = new(
                entities,
                stores,
                new[] { typeof(PositionComponent), typeof(MarkerComponent) },
                new[] { typeof(BlockerComponent) });

            filter.RebuildMatches();

            List<string> snapshot = new();
            for (int i = 0; i < filter.EntityCount; i++)
            {
                EntityHandle entity = filter.GetEntity(i);
                Assert.IsTrue(stores.TryGetComponent(entity.SlotId, out PositionComponent position));
                snapshot.Add($"{entity}:{position.Value}");
            }

            return snapshot;
        }

        private static ComponentStores CreateStores()
        {
            ComponentStores stores = new();
            stores.RegisterStore<PositionComponent>();
            stores.RegisterStore<MarkerComponent>();
            stores.RegisterStore<BlockerComponent>();
            return stores;
        }

        private static Entity CreateEntity(
            Entities entities,
            ComponentStores stores,
            int position,
            bool hasMarker,
            bool hasBlocker)
        {
            Entity entity = entities.Create();
            stores.AddComponent(entity.SlotId, new PositionComponent(position));

            if (hasMarker)
                stores.AddComponent(entity.SlotId, new MarkerComponent());

            if (hasBlocker)
                stores.AddComponent(entity.SlotId, new BlockerComponent());

            return entity;
        }

        private static EntityHandle ToHandle(Entity entity)
        {
            return new EntityHandle(entity.SlotId, entity.SequenceId);
        }

        private sealed class PositionComponent : IComponent
        {
            internal readonly int Value;

            internal PositionComponent(int value)
            {
                Value = value;
            }
        }

        private sealed class MarkerComponent : IComponent
        {
        }

        private sealed class BlockerComponent : IComponent
        {
        }
    }
}

