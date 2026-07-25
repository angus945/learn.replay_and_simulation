using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.World.API;
using SimulationCore.World.Application;
using SimulationCore.World.Contract;

namespace ReplayAndSimulationCore.Test.World.Application
{
    public sealed class EcsWorldApplicationTests
    {
        [Test]
        public void RegisterComponent_WhenSameComponentTypeIsRegisteredTwice_Throws()
        {
            EcsWorld world = CreateWorldWithoutComponents();
            world.RegisterComponent<PositionComponent>();

            Assert.Throws<InvalidOperationException>(() => world.RegisterComponent<PositionComponent>());
        }

        [Test]
        public void RegisterSystem_WhenSystemIsNull_Throws()
        {
            EcsWorld world = CreateWorldWithoutComponents();

            Assert.Throws<ArgumentNullException>(() => world.RegisterSystem<ISystem>(null));
        }

        [Test]
        public void RegisterSystem_WhenSameSystemTypeIsRegisteredTwice_Throws()
        {
            EcsWorld world = CreateWorldWithoutComponents();
            world.RegisterSystem(new FirstSystem(new List<string>()));

            Assert.Throws<InvalidOperationException>(
                () => world.RegisterSystem(new FirstSystem(new List<string>())));
        }

        [Test]
        public void InitializeSystems_WhenSystemsAreRegistered_InvokesThemInRegistrationOrder()
        {
            List<string> trace = new();
            EcsWorld world = CreateWorldWithoutComponents();
            world.RegisterSystem(new FirstSystem(trace));
            world.RegisterSystem(new SecondSystem(trace));

            world.InitializeSystems();

            CollectionAssert.AreEqual(new[] { "init:first", "init:second" }, trace);
        }

        [Test]
        public void InitializeSystems_ReplayingSameRegistrationOrder_ProducesSameTrace()
        {
            List<string> expected = RunSystemInitializationScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunSystemInitializationScenario());
            }
        }

        private static EcsWorld CreateWorldWithoutComponents()
        {
            return new EcsWorld(entityCapacity: 4, new NoopCommandRegistryPort());
        }

        private static List<string> RunSystemInitializationScenario()
        {
            List<string> trace = new();
            EcsWorld world = CreateWorldWithoutComponents();
            world.RegisterSystem(new FirstSystem(trace));
            world.RegisterSystem(new SecondSystem(trace));
            world.InitializeSystems();
            return trace;
        }

        private sealed class NoopCommandRegistryPort : ICommandHandleRegistryPort
        {
            public void Register<TCommand>(ICommandHandler<TCommand> handler)
                where TCommand : ICommand
            {
            }
        }

        private sealed class PositionComponent : IComponent
        {
        }

        private abstract class RecordingSystem : ISystem
        {
            private readonly string label;
            private readonly List<string> trace;

            protected RecordingSystem(string label, List<string> trace)
            {
                this.label = label;
                this.trace = trace;
            }

            public void Initialize(IEcsWorld world, ICommandHandleRegistryPort commandSubscriber)
            {
                trace.Add($"init:{label}");
            }
        }

        private sealed class FirstSystem : RecordingSystem
        {
            internal FirstSystem(List<string> trace)
                : base("first", trace)
            {
            }
        }

        private sealed class SecondSystem : RecordingSystem
        {
            internal SecondSystem(List<string> trace)
                : base("second", trace)
            {
            }
        }
    }
}

