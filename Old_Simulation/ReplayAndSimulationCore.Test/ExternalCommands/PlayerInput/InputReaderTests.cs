using NUnit.Framework;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands.PlayerInput
{
    [TestFixture]
    public sealed class InputReaderTests
    {
        [Test]
        public void ButtonStateReader_AfterPress_EmitsPressedOnceAndKeepsDown()
        {
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);

            ButtonInputState pressedTick = reader.ConsumeTickInput();
            ButtonInputState heldTick = reader.ConsumeTickInput();

            AssertButtonState(pressedTick, true, true, false);
            AssertButtonState(heldTick, false, true, false);
        }

        [Test]
        public void ButtonStateReader_WhenPressAndReleaseBeforeTick_EmitsBothEdges()
        {
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.CaptureRawState(false);

            ButtonInputState state = reader.ConsumeTickInput();

            AssertButtonState(state, true, false, true);
        }

        [TestCase(-2f, -32767)]
        [TestCase(-1f, -32767)]
        [TestCase(-0.5f, -16384)]
        [TestCase(0f, 0)]
        [TestCase(0.5f, 16384)]
        [TestCase(1f, 32767)]
        [TestCase(2f, 32767)]
        public void AxisStateReader_QuantizeAxis_ClampsAndRoundsToShortRange(
            float value,
            int expected)
        {
            Assert.AreEqual((short)expected, AxisStateReader.QuantizeAxis(value));
        }

        [Test]
        public void AxisStateReader_ReadTickInput_AfterMultipleCaptures_ReturnsLatestQuantizedValue()
        {
            AxisStateReader reader = new AxisStateReader();
            reader.CaptureRawState(0.25f);
            reader.CaptureRawState(2f);

            AxisInputEvent input = reader.ReadTickInput();

            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(2f),
                input.Value);
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
