using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationInput.API;
using SimulationInput.Application;
using SimulationInput.Contract;
using SimulationInput.Domain;

namespace SimulationInput.Test.Application
{
    [TestFixture]
    public sealed class SimulationInputsTests
    {
        [Test]
        public void RegisterPullers_BeforeInitialize_ReturnsStableIndependentIndices()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();

            // Act / Assert
            Assert.AreEqual(
                0,
                inputs.RegisterButtonStatePuller<JumpButton>(
                    new TestButtonStatePuller()));
            Assert.AreEqual(
                1,
                inputs.RegisterButtonStatePuller<FireButton>(
                    new TestButtonStatePuller()));
            Assert.AreEqual(
                0,
                inputs.RegisterAxisStatePuller<HorizontalAxis>(
                    new TestAxisStatePuller()));
            Assert.AreEqual(
                1,
                inputs.RegisterAxisStatePuller<VerticalAxis>(
                    new TestAxisStatePuller()));
        }

        [Test]
        public void RegisterNullPuller_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();

            // Act / Assert
            Assert.Throws<ArgumentNullException>(
                () => inputs.RegisterButtonStatePuller<JumpButton>(null));
            Assert.Throws<ArgumentNullException>(
                () => inputs.RegisterAxisStatePuller<HorizontalAxis>(null));
        }

        [Test]
        public void RegisterDuplicateKey_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            inputs.RegisterButtonStatePuller<JumpButton>(
                new TestButtonStatePuller());
            inputs.RegisterAxisStatePuller<HorizontalAxis>(
                new TestAxisStatePuller());

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterButtonStatePuller<JumpButton>(
                    new TestButtonStatePuller()));
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterAxisStatePuller<HorizontalAxis>(
                    new TestAxisStatePuller()));
        }

        [Test]
        public void RegisterAfterInitialize_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            inputs.Initialize();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterButtonStatePuller<JumpButton>(
                    new TestButtonStatePuller()));
            Assert.Throws<InvalidOperationException>(
                () => inputs.RegisterAxisStatePuller<HorizontalAxis>(
                    new TestAxisStatePuller()));
        }

        [Test]
        public void Initialize_CalledTwice_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            inputs.Initialize();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => inputs.Initialize());
        }

        [Test]
        public void CaptureOrConsumeBeforeInitialize_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => inputs.CaptureRenderInput());
            Assert.Throws<InvalidOperationException>(
                () => inputs.ConsumeSnapshot(1));
        }

        [Test]
        public void ConsumeSnapshot_WhenTickDoesNotIncrease_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            inputs.Initialize();
            inputs.ConsumeSnapshot(10);

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => inputs.ConsumeSnapshot(10));
            Assert.Throws<InvalidOperationException>(
                () => inputs.ConsumeSnapshot(9));
        }

        [Test]
        public void CaptureRenderInputAndConsumeSnapshot_TracksButtonAndAxisState()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            TestButtonStatePuller button = new TestButtonStatePuller();
            TestAxisStatePuller axis = new TestAxisStatePuller();
            inputs.RegisterButtonStatePuller<JumpButton>(button);
            inputs.RegisterAxisStatePuller<HorizontalAxis>(axis);
            inputs.Initialize();

            // Act / Assert
            button.IsPressed = true;
            axis.Value = 0.5f;
            inputs.CaptureRenderInput();
            IInputSnapshot pressedSnapshot = inputs.ConsumeSnapshot(1);
            AssertButtonState(
                pressedSnapshot.GetButtonState<JumpButton>(),
                true,
                true,
                false);
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(0.5f),
                pressedSnapshot.GetAxisState<HorizontalAxis>().Value);

            IInputSnapshot heldSnapshot = inputs.ConsumeSnapshot(2);
            AssertButtonState(
                heldSnapshot.GetButtonState<JumpButton>(),
                false,
                true,
                false);
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(0.5f),
                heldSnapshot.GetAxisState<HorizontalAxis>().Value);

            button.IsPressed = false;
            axis.Value = 2f;
            inputs.CaptureRenderInput();
            IInputSnapshot releasedSnapshot = inputs.ConsumeSnapshot(3);
            AssertButtonState(
                releasedSnapshot.GetButtonState<JumpButton>(),
                false,
                false,
                true);
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(2f),
                releasedSnapshot.GetAxisState<HorizontalAxis>().Value);
        }

        [Test]
        public void CaptureRenderInput_WhenPressAndReleaseBeforeTick_EmitsBothEdges()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            TestButtonStatePuller button = new TestButtonStatePuller();
            inputs.RegisterButtonStatePuller<JumpButton>(button);
            inputs.Initialize();

            // Act
            button.IsPressed = true;
            inputs.CaptureRenderInput();
            button.IsPressed = false;
            inputs.CaptureRenderInput();
            IInputSnapshot snapshot = inputs.ConsumeSnapshot(1);

            // Assert
            AssertButtonState(
                snapshot.GetButtonState<JumpButton>(),
                true,
                false,
                true);
        }

        [Test]
        public void Snapshot_WhenInputKeyIsUnregistered_Throws()
        {
            // Arrange
            SimulationInputs inputs = new SimulationInputs();
            inputs.RegisterButtonStatePuller<JumpButton>(
                new TestButtonStatePuller());
            inputs.Initialize();
            IInputSnapshot snapshot = inputs.ConsumeSnapshot(1);

            // Act / Assert
            Assert.Throws<KeyNotFoundException>(
                () => snapshot.GetButtonState<FireButton>());
            Assert.Throws<KeyNotFoundException>(
                () => snapshot.GetAxisState<HorizontalAxis>());
        }

        private static void AssertButtonState(
            ButtonState state,
            bool isPressed,
            bool isDown,
            bool isReleased)
        {
            Assert.AreEqual(isPressed, state.IsPressed);
            Assert.AreEqual(isDown, state.IsDown);
            Assert.AreEqual(isReleased, state.IsReleased);
        }

        private sealed class TestButtonStatePuller : IButtonStatePuller
        {
            public bool IsPressed { get; set; }
        }

        private sealed class TestAxisStatePuller : IAxisStatePuller
        {
            public float Value { get; set; }
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
