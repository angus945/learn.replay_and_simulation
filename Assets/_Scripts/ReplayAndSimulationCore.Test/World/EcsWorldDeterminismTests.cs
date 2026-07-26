using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;

namespace ReplayAndSimulationCore.Test.World
{
    public sealed class EcsWorldDeterminismTests
    {
        [Test]
        public void CommitStructuralChanges_ReplayingSameSpawnSequence_ProducesSameEntitySnapshot()
        {
            List<string> expected = RunSpawnScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunSpawnScenario());
            }
        }

        [Test]
        public void RuntimeTicks_ReplayingSameTickSequence_DispatchesSystemsInDeterministicOrder()
        {
            string[] expected =
            {
                "init:first",
                "init:second",
                "pre:first:1:0.016",
                "pre:second:1:0.016",
                "post:first:1:0.016",
                "post:second:1:0.016",
                "pre:first:2:0.016",
                "pre:second:2:0.016",
                "post:first:2:0.016",
                "post:second:2:0.016"
            };

            for (int i = 0; i < 10; i++)
            {
                CollectionAssert.AreEqual(expected, RunRuntimeScenario());
            }
        }

        private static List<string> RunSpawnScenario()
        {
            EcsWorld world = CreateWorld();
            IEcsWorld ecsWorld = world;
            ISimulationWorld simulationWorld = world;
            IEntityFilter positions = ecsWorld.CreateFilter()
                .With<PositionComponent>()
                .Build();

            ecsWorld.SpawnRequest(new TestEntityRecipe(), new TestEntityArguments(1, 1, hasMarker: false));
            ecsWorld.SpawnRequest(new TestEntityRecipe(), new TestEntityArguments(2, 3, hasMarker: true));
            ecsWorld.SpawnRequest(new TestEntityRecipe(), new TestEntityArguments(5, 8, hasMarker: false));
            simulationWorld.CommitStructuralChanges();

            EntityHandle secondEntity = positions.GetEntity(1);
            ecsWorld.SetComponent(secondEntity, new PositionComponent(13, 21));

            List<string> snapshot = new();
            for (int i = 0; i < positions.EntityCount; i++)
            {
                EntityHandle entity = positions.GetEntity(i);
                Assert.IsTrue(ecsWorld.TryGetComponent(entity, out PositionComponent position));
                bool hasMarker = ecsWorld.TryGetComponent(entity, out MarkerComponent _);
                snapshot.Add($"{entity}:{position.X}:{position.Y}:{hasMarker}");
            }

            return snapshot;
        }

        private static List<string> RunRuntimeScenario()
        {
            List<string> trace = new();
            EcsWorld world = new(entityCapacity: 4, new NoopCommandRegistryPort());
            world.RegisterSystem(new FirstTickSystem(trace));
            world.RegisterSystem(new SecondTickSystem(trace));
            world.InitializeSystems();

            ISimulationWorld simulationWorld = world;
            simulationWorld.PrePhysicsTick(1, 0.016f);
            simulationWorld.PostPhysicsTick(1, 0.016f);
            simulationWorld.PrePhysicsTick(2, 0.016f);
            simulationWorld.PostPhysicsTick(2, 0.016f);

            return trace;
        }

        private static EcsWorld CreateWorld()
        {
            EcsWorld world = new(entityCapacity: 8, new NoopCommandRegistryPort());
            world.RegisterComponent<PositionComponent>();
            world.RegisterComponent<MarkerComponent>();
            return world;
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

                if (arguments.HasMarker)
                    context.AddComponent(new MarkerComponent());
            }
        }

        private readonly struct TestEntityArguments : IEntityArguments
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly bool HasMarker;

            internal TestEntityArguments(int x, int y, bool hasMarker)
            {
                X = x;
                Y = y;
                HasMarker = hasMarker;
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

        private sealed class MarkerComponent : IComponent
        {
        }

        private abstract class RecordingTickSystem : ISystem, IPrePhysicsTick, IPostPhysicsTick
        {
            private readonly string label;
            private readonly List<string> trace;

            protected RecordingTickSystem(string label, List<string> trace)
            {
                this.label = label;
                this.trace = trace;
            }

            public void Initialize(IEcsWorld world, ICommandHandleRegistryPort commandSubscriber)
            {
                trace.Add($"init:{label}");
            }

            public void PrePhysicsTick(ulong tick, float deltaTime)
            {
                trace.Add($"pre:{label}:{tick}:{deltaTime:0.000}");
            }

            public void PostPhysicsTick(ulong tick, float deltaTime)
            {
                trace.Add($"post:{label}:{tick}:{deltaTime:0.000}");
            }
        }

        private sealed class FirstTickSystem : RecordingTickSystem
        {
            internal FirstTickSystem(List<string> trace)
                : base("first", trace)
            {
            }
        }

        private sealed class SecondTickSystem : RecordingTickSystem
        {
            internal SecondTickSystem(List<string> trace)
                : base("second", trace)
            {
            }
        }
    }
}

