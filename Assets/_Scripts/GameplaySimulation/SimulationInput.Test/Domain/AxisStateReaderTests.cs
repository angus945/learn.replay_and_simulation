using NUnit.Framework;
using SimulationInput.Domain;

namespace SimulationInput.Test.Domain
{
    [TestFixture]
    public sealed class AxisStateReaderTests
    {
        [TestCase(-2f, -32767)]
        [TestCase(-1f, -32767)]
        [TestCase(-0.5f, -16384)]
        [TestCase(0f, 0)]
        [TestCase(0.5f, 16384)]
        [TestCase(1f, 32767)]
        [TestCase(2f, 32767)]
        public void QuantizeAxis_ClampsAndRoundsToShortRange(
            float value,
            int expected)
        {
            Assert.AreEqual((short)expected, AxisStateReader.QuantizeAxis(value));
        }

        [Test]
        public void ReadTickInput_BeforeCapture_ReturnsZero()
        {
            // Arrange
            AxisStateReader reader = new AxisStateReader();

            // Act
            AxisInputEvent input = reader.ReadTickInput();

            // Assert
            Assert.AreEqual(0f, input.Value);
        }

        [Test]
        public void ReadTickInput_AfterCapture_ReturnsLatestQuantizedValue()
        {
            // Arrange
            AxisStateReader reader = new AxisStateReader();
            reader.CaptureRawState(0.25f);
            reader.CaptureRawState(2f);

            // Act
            AxisInputEvent input = reader.ReadTickInput();

            // Assert
            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(2f),
                input.Value);
        }
    }
}
