using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;

namespace ReplayAndSimulationCore.Test.World
{
    public sealed class EcsWorldBasicTests
    {
        [Test]
        public void CommitStructuralChanges_WhenSpawnRequested_CreatesEntityWithRecipeComponents()
        {
            EcsWorld world = CreateWorld();
            IEcsWorld ecsWorld = world;
            ISimulationWorld simulationWorld = world;
            IEntityFilter movers = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .With<VelocityComponent>()
                .Build();

            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(3, 4, 1, 2, hasVelocity: true, isBlocked: false));

            Assert.AreEqual(0, movers.EntityCount);

            simulationWorld.CommitStructuralChanges();

            Assert.AreEqual(1, movers.EntityCount);
            EntityHandle entity = movers.GetEntity(0);

            Assert.IsTrue(ecsWorld.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(3, position.X);
            Assert.AreEqual(4, position.Y);

            Assert.IsTrue(ecsWorld.TryGetComponent(entity, out VelocityComponent velocity));
            Assert.AreEqual(1, velocity.X);
            Assert.AreEqual(2, velocity.Y);
        }

        [Test]
        public void Filter_WhenWithAndWithoutAreUsed_MatchesCommittedEntitiesInSpawnOrder()
        {
            EcsWorld world = CreateWorld();
            IEcsWorld ecsWorld = world;
            ISimulationWorld simulationWorld = world;
            IEntityFilter unblockedPositions = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .Without<BlockerComponent>()
                .Build();
            IEntityFilter movers = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .With<VelocityComponent>()
                .Build();

            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(10, 0, 1, 0, hasVelocity: true, isBlocked: false));
            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(20, 0, 0, 0, hasVelocity: false, isBlocked: true));
            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(30, 0, 3, 0, hasVelocity: true, isBlocked: true));

            simulationWorld.CommitStructuralChanges();

            Assert.AreEqual(1, unblockedPositions.EntityCount);
            Assert.AreEqual(2, movers.EntityCount);

            AssertPosition(ecsWorld, unblockedPositions.GetEntity(0), 10, 0);
            AssertPosition(ecsWorld, movers.GetEntity(0), 10, 0);
            AssertPosition(ecsWorld, movers.GetEntity(1), 30, 0);
        }

        [Test]
        public void SetComponent_WhenEntityIsAlive_UpdatesExistingComponent()
        {
            EcsWorld world = CreateWorld();
            IEcsWorld ecsWorld = world;
            ISimulationWorld simulationWorld = world;
            IEntityFilter positions = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .Build();

            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(1, 2, 0, 0, hasVelocity: false, isBlocked: false));
            simulationWorld.CommitStructuralChanges();

            EntityHandle entity = positions.GetEntity(0);
            ecsWorld.SetComponent(entity, new PositionComponent(7, 8));

            AssertPosition(ecsWorld, entity, 7, 8);
        }

        [Test]
        public void CommitStructuralChanges_WhenDestroyRequested_RemovesEntityAndComponents()
        {
            EcsWorld world = CreateWorld();
            IEcsWorld ecsWorld = world;
            ISimulationWorld simulationWorld = world;
            IEntityFilter positions = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .Build();

            ecsWorld.SpawnRequest(
                new TestEntityRecipe(),
                new TestEntityArguments(5, 6, 0, 0, hasVelocity: false, isBlocked: false));
            simulationWorld.CommitStructuralChanges();

            EntityHandle entity = positions.GetEntity(0);
            Assert.IsTrue(ecsWorld.TryGetComponent(entity, out PositionComponent _));

            ecsWorld.DestroyRequest(entity);
            simulationWorld.CommitStructuralChanges();

            Assert.AreEqual(0, positions.EntityCount);
            Assert.IsFalse(ecsWorld.TryGetComponent(entity, out PositionComponent _));
            Assert.IsFalse(positions.Contains(entity));
        }

        private static EcsWorld CreateWorld()
        {
            EcsWorld world = new(entityCapacity: 8, new NoopCommandRegistryPort());
            world.RegisterComponent<PositionComponent>();
            world.RegisterComponent<VelocityComponent>();
            world.RegisterComponent<BlockerComponent>();
            return world;
        }

        private static void AssertPosition(IEcsWorld world, EntityHandle entity, int x, int y)
        {
            Assert.IsTrue(world.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(x, position.X);
            Assert.AreEqual(y, position.Y);
        }

        private sealed class NoopCommandRegistryPort : ICommandHandleRegistryPort
        {
            public void Register<TCommand>(ICommandHandler<TCommand> handler)
                where TCommand : ICommand
            {
            }
        }

        private sealed class TestEntityRecipe : IEntityRecipe<TestEntityArguments>
        {
            public void Build(IEntityBuildContext context, in TestEntityArguments arguments)
            {
                context.AddComponent(new PositionComponent(arguments.X, arguments.Y));

                if (arguments.HasVelocity)
                    context.AddComponent(new VelocityComponent(arguments.VelocityX, arguments.VelocityY));

                if (arguments.IsBlocked)
                    context.AddComponent(new BlockerComponent());
            }
        }

        private readonly struct TestEntityArguments : IEntityArguments
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int VelocityX;
            internal readonly int VelocityY;
            internal readonly bool HasVelocity;
            internal readonly bool IsBlocked;

            internal TestEntityArguments(
                int x,
                int y,
                int velocityX,
                int velocityY,
                bool hasVelocity,
                bool isBlocked)
            {
                X = x;
                Y = y;
                VelocityX = velocityX;
                VelocityY = velocityY;
                HasVelocity = hasVelocity;
                IsBlocked = isBlocked;
            }
        }

        private sealed class PositionComponent : IComponent
        {
            internal readonly int X;
            internal readonly int Y;

            internal PositionComponent(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private sealed class VelocityComponent : IComponent
        {
            internal readonly int X;
            internal readonly int Y;

            internal VelocityComponent(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private sealed class BlockerComponent : IComponent
        {
        }
    }
}

