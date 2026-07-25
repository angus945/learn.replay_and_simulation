using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands.Domain
{
    [TestFixture]
    public sealed class ButtonStateReaderTests
    {
        [Test]
        public void ConsumeTickInput_BeforeCapture_ReturnsDefaultState()
        {
            ButtonStateReader reader = new ButtonStateReader();

            ButtonInputState state = reader.ConsumeTickInput();

            AssertButtonState(state, false, false, false);
        }

        [Test]
        public void ConsumeTickInput_AfterRelease_EmitsReleasedOnceAndClearsDown()
        {
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.ConsumeTickInput();
            reader.CaptureRawState(false);

            ButtonInputState releasedTick = reader.ConsumeTickInput();
            ButtonInputState idleTick = reader.ConsumeTickInput();

            AssertButtonState(releasedTick, false, false, true);
            AssertButtonState(idleTick, false, false, false);
        }

        [Test]
        public void CaptureRawState_RepeatedSameState_DoesNotEmitExtraEdges()
        {
            ButtonStateReader reader = new ButtonStateReader();
            reader.CaptureRawState(true);
            reader.CaptureRawState(true);
            reader.ConsumeTickInput();

            reader.CaptureRawState(true);
            ButtonInputState state = reader.ConsumeTickInput();

            AssertButtonState(state, false, true, false);
        }

        [Test]
        public void ConsumeTickInput_ReplayingSameRawStateScript_ProducesSameTickStates()
        {
            CollectionAssert.AreEqual(
                RunButtonScript(),
                RunButtonScript());
        }

        private static string[] RunButtonScript()
        {
            ButtonStateReader reader = new ButtonStateReader();
            List<string> trace = new List<string>();

            reader.CaptureRawState(true);
            trace.Add(ToSignature(reader.ConsumeTickInput()));

            reader.CaptureRawState(true);
            trace.Add(ToSignature(reader.ConsumeTickInput()));

            reader.CaptureRawState(false);
            reader.CaptureRawState(true);
            reader.CaptureRawState(false);
            trace.Add(ToSignature(reader.ConsumeTickInput()));

            trace.Add(ToSignature(reader.ConsumeTickInput()));

            return trace.ToArray();
        }

        private static string ToSignature(ButtonInputState state)
        {
            return $"{state.IsPressed}|{state.IsDown}|{state.IsReleased}";
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
