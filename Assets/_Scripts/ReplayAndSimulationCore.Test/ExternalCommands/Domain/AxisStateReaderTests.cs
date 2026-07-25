using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands.Domain
{
    [TestFixture]
    public sealed class AxisStateReaderTests
    {
        [Test]
        public void ReadTickInput_BeforeCapture_ReturnsZero()
        {
            AxisStateReader reader = new AxisStateReader();

            AxisInputEvent input = reader.ReadTickInput();

            Assert.AreEqual(0f, input.Value);
        }

        [Test]
        public void ReadTickInput_AfterMultipleCaptures_ReturnsLatestQuantizedValue()
        {
            AxisStateReader reader = new AxisStateReader();
            reader.CaptureRawState(0.25f);
            reader.CaptureRawState(2f);

            AxisInputEvent input = reader.ReadTickInput();

            Assert.AreEqual(
                (float)AxisStateReader.QuantizeAxis(2f),
                input.Value);
        }

        [Test]
        public void ReadTickInput_ReplayingSameRawAxisScript_ProducesSameTickValues()
        {
            CollectionAssert.AreEqual(
                RunAxisScript(),
                RunAxisScript());
        }

        private static string[] RunAxisScript()
        {
            AxisStateReader reader = new AxisStateReader();
            List<string> trace = new List<string>();
            float[] values = { -2f, -0.5f, 0f, 0.3333f, 1.5f, -0.125f };

            for (int i = 0; i < values.Length; i++)
            {
                reader.CaptureRawState(values[i]);
                trace.Add(reader.ReadTickInput().Value.ToString(CultureInfo.InvariantCulture));
            }

            return trace.ToArray();
        }
    }
}
