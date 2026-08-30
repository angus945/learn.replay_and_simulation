using System;
using System.Collections.Generic;
using NUnit.Framework;
using ReplayAndSimulationCore.Test.ExternalCommands;
using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands.Application
{
    [TestFixture]
    public sealed class FrameSnapShotTests
    {
        [Test]
        public void GetInputState_ReturnsFrameValuesByRegisteredKey()
        {
            FrameSnapShot snapshot = CreateSnapshot(
                new ButtonInputState(true, true, false),
                new AxisInputEvent(1024f));

            ButtonState button = snapshot.GetButtonState<JumpButton>();
            AxisState axis = snapshot.GetAxisState<HorizontalAxis>();

            Assert.AreEqual(true, button.IsPressed);
            Assert.AreEqual(true, button.IsDown);
            Assert.AreEqual(false, button.IsReleased);
            Assert.AreEqual(1024f, axis.Value);
        }

        [Test]
        public void GetInputState_WhenInputKeyIsUnregistered_Throws()
        {
            FrameSnapShot snapshot = CreateSnapshot(
                new ButtonInputState(true, true, false),
                new AxisInputEvent(1024f));

            Assert.Throws<KeyNotFoundException>(
                () => snapshot.GetButtonState<FireButton>());
            Assert.Throws<KeyNotFoundException>(
                () => snapshot.GetAxisState<VerticalAxis>());
        }

        [Test]
        public void GetInputState_ReplayingSameReadSequence_ProducesSameValues()
        {
            CollectionAssert.AreEqual(
                RunSnapshotReadScript(),
                RunSnapshotReadScript());
        }

        private static string[] RunSnapshotReadScript()
        {
            FrameSnapShot snapshot = CreateSnapshot(
                new ButtonInputState(true, false, true),
                new AxisInputEvent(-256f));

            ButtonState firstButton = snapshot.GetButtonState<JumpButton>();
            AxisState firstAxis = snapshot.GetAxisState<HorizontalAxis>();
            ButtonState secondButton = snapshot.GetButtonState<JumpButton>();
            AxisState secondAxis = snapshot.GetAxisState<HorizontalAxis>();

            return new[]
            {
                ToSignature(firstButton),
                firstAxis.Value.ToString(),
                ToSignature(secondButton),
                secondAxis.Value.ToString()
            };
        }

        private static FrameSnapShot CreateSnapshot(
            ButtonInputState button,
            AxisInputEvent axis)
        {
            TickInputFrame frame = TestFrameFactory.CreateFrame(
                new Dictionary<Type, int>
                {
                    { typeof(JumpButton), 0 }
                },
                new Dictionary<Type, int>
                {
                    { typeof(HorizontalAxis), 0 }
                },
                new[] { button },
                new[] { axis });

            return new FrameSnapShot(frame);
        }

        private static string ToSignature(ButtonState state)
        {
            return $"{state.IsPressed}|{state.IsDown}|{state.IsReleased}";
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
