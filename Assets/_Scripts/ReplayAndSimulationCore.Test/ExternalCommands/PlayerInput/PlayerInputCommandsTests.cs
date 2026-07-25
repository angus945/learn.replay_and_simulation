using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;
using SimulationCore.ExternalCommands.PlayerInput.Infrastructure;
using SimulationCore.ExternalCommands.Port;

namespace ReplayAndSimulationCore.Test.ExternalCommands.PlayerInput
{
    [TestFixture]
    public sealed class PlayerInputCommandsTests
    {
        [Test]
        public void RegisterPullers_BeforeInitialize_ReturnsStableIndependentIndices()
        {
            PlayerInputCommands inputs = CreateInputs(out _, out _, out _, out _);

            Assert.AreEqual(
                0,
                inputs.RegisterButtonStatePuller<JumpButton>(new TestButtonStatePuller()));
            Assert.AreEqual(
                1,
                inputs.RegisterButtonStatePuller<FireButton>(new TestButtonStatePuller()));
            Assert.AreEqual(
                0,
                inputs.RegisterAxisStatePuller<HorizontalAxis>(new TestAxisStatePuller()));
            Assert.AreEqual(
                1,
                inputs.RegisterAxisStatePuller<VerticalAxis>(new TestAxisStatePuller()));
        }

        [Test]
        public void RegisterDuplicateInputKeys_Throws()
        {
            PlayerInputCommands inputs = CreateInputs(out _, out _, out _, out _);
            inputs.RegisterButtonStatePuller<JumpButton>(new TestButtonStatePuller());
            inputs.RegisterAxisStatePuller<HorizontalAxis>(new TestAxisStatePuller());

            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterButtonStatePuller<JumpButton>(
                    new TestButtonStatePuller()));
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterAxisStatePuller<HorizontalAxis>(
                    new TestAxisStatePuller()));
        }

        [Test]
        public void RegisterDuplicateCommandRuleType_Throws()
        {
            PlayerInputCommands inputs = CreateInputs(out _, out _, out _, out _);
            inputs.RegisterInputCommand<PlayerMovementCommand>(new MovementRule());

            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterInputCommand<PlayerMovementCommand>(
                    new MovementRule()));
        }

        [Test]
        public void RegisterPuller_AfterInitialize_ThrowsToKeepRuntimeFrameShapeStable()
        {
            PlayerInputCommands inputs = CreateInputs(out _, out _, out _, out _);
            inputs.Initialize();

            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterButtonStatePuller<JumpButton>(
                    new TestButtonStatePuller()));
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterAxisStatePuller<HorizontalAxis>(
                    new TestAxisStatePuller()));
        }

        [Test]
        public void CaptureRenderInputAndEnqueueCommands_EnqueuesInputCommandWithMetadata()
        {
            PlayerInputCommands inputs = CreateInitializedMovementInputs(
                out TestButtonStatePuller button,
                out TestAxisStatePuller axis,
                out RecordingCommandPort commandPort);

            button.IsPressed = true;
            axis.Value = 0.5f;
            inputs.CaptureRenderInput();
            inputs.EnqueueCommands(42);

            Assert.AreEqual(1, commandPort.Records.Count);
            Assert.AreEqual(42ul, commandPort.Records[0].Metadata.Tick);
            Assert.IsTrue(commandPort.Records[0].Metadata.IsExternal);
            Assert.AreEqual(CommandSource.Input, commandPort.Records[0].Metadata.Source);

            PlayerMovementCommand command =
                (PlayerMovementCommand)commandPort.Records[0].Command;
            Assert.AreEqual(true, command.IsPressed);
            Assert.AreEqual(true, command.IsDown);
            Assert.AreEqual(false, command.IsReleased);
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(0.5f),
                command.AxisValue);
        }

        [Test]
        public void CaptureRenderInput_WhenPressAndReleaseBeforeTick_EnqueuesBothButtonEdges()
        {
            PlayerInputCommands inputs = CreateInitializedMovementInputs(
                out TestButtonStatePuller button,
                out TestAxisStatePuller axis,
                out RecordingCommandPort commandPort);

            button.IsPressed = true;
            axis.Value = 0.25f;
            inputs.CaptureRenderInput();
            button.IsPressed = false;
            axis.Value = -0.25f;
            inputs.CaptureRenderInput();
            inputs.EnqueueCommands(7);

            Assert.AreEqual(1, commandPort.Records.Count);
            PlayerMovementCommand command =
                (PlayerMovementCommand)commandPort.Records[0].Command;
            Assert.AreEqual(true, command.IsPressed);
            Assert.AreEqual(false, command.IsDown);
            Assert.AreEqual(true, command.IsReleased);
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(-0.25f),
                command.AxisValue);
        }

        [Test]
        public void EnqueueCommands_RunsRegisteredRulesInRegistrationOrder()
        {
            PlayerInputCommands inputs = CreateInputs(
                out _,
                out _,
                out RuleRegistration rules,
                out RecordingCommandPort commandPort);
            inputs.RegisterInputCommand<FirstRuleCommand>(
                new ConstantRule(new FirstRuleCommand()));
            inputs.RegisterInputCommand<SecondRuleCommand>(
                new ConstantRule(new SecondRuleCommand()));
            inputs.Initialize();

            inputs.EnqueueCommands(3);

            Assert.AreEqual(2, commandPort.Records.Count);
            Assert.IsInstanceOf<FirstRuleCommand>(commandPort.Records[0].Command);
            Assert.IsInstanceOf<SecondRuleCommand>(commandPort.Records[1].Command);
            Assert.AreEqual(2, rules.RuleCount);
        }

        [Test]
        public void EnqueueCommands_WhenSameInputScriptRunsTwice_ProducesSameCommandSequence()
        {
            CollectionAssert.AreEqual(
                RunInputScript(),
                RunInputScript());
        }

        private static PlayerInputCommands CreateInputs(
            out ButtonRegistration buttonRegistration,
            out AxisRegistration axisRegistration,
            out RuleRegistration ruleRegistration,
            out RecordingCommandPort commandPort)
        {
            buttonRegistration = new ButtonRegistration();
            axisRegistration = new AxisRegistration();
            ruleRegistration = new RuleRegistration();
            commandPort = new RecordingCommandPort();
            return new PlayerInputCommands(
                commandPort,
                buttonRegistration,
                axisRegistration,
                ruleRegistration);
        }

        private static PlayerInputCommands CreateInitializedMovementInputs(
            out TestButtonStatePuller button,
            out TestAxisStatePuller axis,
            out RecordingCommandPort commandPort)
        {
            PlayerInputCommands inputs = CreateInputs(out _, out _, out _, out commandPort);
            button = new TestButtonStatePuller();
            axis = new TestAxisStatePuller();

            Assert.AreEqual(0, inputs.RegisterButtonStatePuller<JumpButton>(button));
            Assert.AreEqual(0, inputs.RegisterAxisStatePuller<HorizontalAxis>(axis));
            inputs.RegisterInputCommand<PlayerMovementCommand>(new MovementRule());
            inputs.Initialize();

            return inputs;
        }

        private static string[] RunInputScript()
        {
            PlayerInputCommands inputs = CreateInitializedMovementInputs(
                out TestButtonStatePuller button,
                out TestAxisStatePuller axis,
                out RecordingCommandPort commandPort);

            button.IsPressed = true;
            axis.Value = 0.5f;
            inputs.CaptureRenderInput();
            inputs.EnqueueCommands(100);

            inputs.EnqueueCommands(101);

            button.IsPressed = false;
            axis.Value = -0.25f;
            inputs.CaptureRenderInput();
            inputs.EnqueueCommands(102);

            button.IsPressed = true;
            axis.Value = 0.125f;
            inputs.CaptureRenderInput();
            button.IsPressed = false;
            axis.Value = -0.75f;
            inputs.CaptureRenderInput();
            inputs.EnqueueCommands(103);

            return commandPort.ToSignatures();
        }

        private sealed class RecordingCommandPort : ICommandPort
        {
            public readonly List<RecordedCommand> Records = new List<RecordedCommand>();

            public void EnqueueCommand<T>(CommandMetadata commandData, T command) where T : ICommand
            {
                Records.Add(new RecordedCommand(commandData, command));
            }

            public void EnqueueEvent<T>(CommandMetadata eventData, T @event) where T : IEvent
            {
                Records.Add(new RecordedCommand(eventData, @event));
            }

            public string[] ToSignatures()
            {
                string[] signatures = new string[Records.Count];
                for (int i = 0; i < Records.Count; i++)
                {
                    signatures[i] = Records[i].ToSignature();
                }

                return signatures;
            }
        }

        private sealed class RecordedCommand
        {
            public readonly CommandMetadata Metadata;
            public readonly ICommand Command;

            public RecordedCommand(CommandMetadata metadata, ICommand command)
            {
                Metadata = metadata;
                Command = command;
            }

            public string ToSignature()
            {
                if (Command is PlayerMovementCommand movement)
                {
                    return string.Join(
                        "|",
                        Metadata.Tick.ToString(CultureInfo.InvariantCulture),
                        Metadata.IsExternal.ToString(),
                        Metadata.Source.ToString(),
                        movement.IsPressed.ToString(),
                        movement.IsDown.ToString(),
                        movement.IsReleased.ToString(),
                        movement.AxisValue.ToString(CultureInfo.InvariantCulture));
                }

                return string.Join(
                    "|",
                    Metadata.Tick.ToString(CultureInfo.InvariantCulture),
                    Metadata.IsExternal.ToString(),
                    Metadata.Source.ToString(),
                    Command.GetType().FullName);
            }
        }

        private sealed class TestButtonStatePuller : IButtonStatePuller
        {
            public bool IsPressed { get; set; }
        }

        private sealed class TestAxisStatePuller : IAxisStatePuller
        {
            public float Value { get; set; }
        }

        private sealed class MovementRule : IInputCommandRule
        {
            public bool TryProduce(
                IPlayerInputSnapshot snapshot,
                out ICommand command)
            {
                ButtonState button = snapshot.GetButtonState<JumpButton>();
                AxisState axis = snapshot.GetAxisState<HorizontalAxis>();

                if (!button.IsPressed &&
                    !button.IsDown &&
                    !button.IsReleased &&
                    axis.Value == 0f)
                {
                    command = null;
                    return false;
                }

                command = new PlayerMovementCommand(
                    button.IsPressed,
                    button.IsDown,
                    button.IsReleased,
                    axis.Value);
                return true;
            }
        }

        private sealed class ConstantRule : IInputCommandRule
        {
            private readonly ICommand producedCommand;

            public ConstantRule(ICommand producedCommand)
            {
                this.producedCommand = producedCommand;
            }

            public bool TryProduce(
                IPlayerInputSnapshot snapshot,
                out ICommand command)
            {
                command = producedCommand;
                return true;
            }
        }

        private readonly struct PlayerMovementCommand : ICommand
        {
            public readonly bool IsPressed;
            public readonly bool IsDown;
            public readonly bool IsReleased;
            public readonly float AxisValue;

            public PlayerMovementCommand(
                bool isPressed,
                bool isDown,
                bool isReleased,
                float axisValue)
            {
                IsPressed = isPressed;
                IsDown = isDown;
                IsReleased = isReleased;
                AxisValue = axisValue;
            }
        }

        private readonly struct FirstRuleCommand : ICommand
        {
        }

        private readonly struct SecondRuleCommand : ICommand
        {
        }

        private readonly struct JumpButton : IButtonInputKey
        {
        }

        private readonly struct FireButton : IButtonInputKey
        {
        }

        private readonly struct HorizontalAxis : IAxisInputKey
        {
        }

        private readonly struct VerticalAxis : IAxisInputKey
        {
        }
    }
}
