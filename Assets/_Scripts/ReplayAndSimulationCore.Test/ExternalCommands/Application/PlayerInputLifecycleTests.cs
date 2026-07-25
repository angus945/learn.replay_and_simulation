using System;
using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Infrastructure;
using SimulationCore.ExternalCommands.Port;

namespace ReplayAndSimulationCore.Test.ExternalCommands.Application
{
    [TestFixture]
    public sealed class PlayerInputLifecycleTests
    {
        [Test]
        public void Initialize_CalledTwice_ThrowsToKeepApplicationLifecycleSinglePass()
        {
            PlayerInputCommands inputs = CreateInputs();
            inputs.Initialize();

            Assert.Throws<InvalidOperationException>(() => inputs.Initialize());
        }

        [Test]
        public void CaptureRenderInput_BeforeInitialize_ThrowsToPreventImplicitRuntimeState()
        {
            PlayerInputCommands inputs = CreateInputs();
            inputs.RegisterButtonStatePuller<JumpButton>(new TestButtonStatePuller());

            Assert.Throws<InvalidOperationException>(() => inputs.CaptureRenderInput());
        }

        [Test]
        public void EnqueueCommands_BeforeInitialize_ThrowsToPreventNullRuntimeFrame()
        {
            PlayerInputCommands inputs = CreateInputs();
            inputs.RegisterInputCommand<TraceCommand>(new AlwaysProduceRule());

            Assert.Throws<InvalidOperationException>(() => inputs.EnqueueCommands(1));
        }

        private static PlayerInputCommands CreateInputs()
        {
            return new PlayerInputCommands(
                new NullCommandPort(),
                new ButtonRegistration(),
                new AxisRegistration(),
                new RuleRegistration());
        }

        private sealed class NullCommandPort : ICommandEnqueuePort
        {
            public void EnqueueCommands(CommandMetadata commandData, ICommand commandQueue)
            {
            }
        }

        private sealed class TestButtonStatePuller : IButtonStatePuller
        {
            public bool IsPressed { get; set; }
        }

        private sealed class AlwaysProduceRule : IInputCommandRule
        {
            public bool TryProduce(
                IPlayerInputSnapshot snapshot,
                out ICommand command)
            {
                command = new TraceCommand();
                return true;
            }
        }

        private readonly struct TraceCommand : ICommand
        {
        }

        private readonly struct JumpButton : IButtonInputKey
        {
        }
    }
}
