using NUnit.Framework;
using SimulationInput.Domain;

namespace SimulationInput.Test.Domain
{
    [TestFixture]
    public sealed class ButtonStateReaderTests
    {
        [Test]
        public void ConsumeTickInput_BeforeCapture_ReturnsDefaultState()
        {
            // Arrange
            ButtonStateReader reader = new ButtonStateReader();

            // Act
            ButtonInputState state = reader.ConsumeTickInput();

            // Assert
            AssertButtonState(state, false, false, false);
        }

        [Test]
        public void ConsumeTickInput_AfterPress_EmitsPressedOnceAndKeepsDown()
        {
            // Arrange
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);

            // Act
            ButtonInputState pressedTick = reader.ConsumeTickInput();
            ButtonInputState heldTick = reader.ConsumeTickInput();

            // Assert
            AssertButtonState(pressedTick, true, true, false);
            AssertButtonState(heldTick, false, true, false);
        }

        [Test]
        public void ConsumeTickInput_AfterRelease_EmitsReleasedOnceAndClearsDown()
        {
            // Arrange
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.ConsumeTickInput();
            reader.CaptureRawState(false);

            // Act
            ButtonInputState releasedTick = reader.ConsumeTickInput();
            ButtonInputState idleTick = reader.ConsumeTickInput();

            // Assert
            AssertButtonState(releasedTick, false, false, true);
            AssertButtonState(idleTick, false, false, false);
        }

        [Test]
        public void ConsumeTickInput_WhenPressAndReleaseBeforeTick_EmitsBothEdges()
        {
            // Arrange
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.CaptureRawState(false);

            // Act
            ButtonInputState state = reader.ConsumeTickInput();

            // Assert
            AssertButtonState(state, true, false, true);
        }

        [Test]
        public void CaptureRawState_RepeatedSameState_DoesNotEmitExtraEdges()
        {
            // Arrange
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.CaptureRawState(true);
            reader.ConsumeTickInput();

            // Act
            reader.CaptureRawState(true);
            ButtonInputState state = reader.ConsumeTickInput();

            // Assert
            AssertButtonState(state, false, true, false);
        }

        private static void AssertButtonState(
            ButtonInputState state,
            bool isPressed,
            bool isDown,
            bool isReleased)
        {
            Assert.AreEqual(isPressed, state.IsPressed);
            Assert.AreEqual(isDown, state.IsDown);
            Assert.AreEqual(isReleased, state.IsReleased);
        }
    }
}
