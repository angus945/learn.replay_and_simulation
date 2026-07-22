using System;
using SimulationInput;

namespace SimulationInput.Domain
{
    public sealed class AxisStateReader
    {
        private float latestValue;

        public void CaptureRawState(float value)
        {
            latestValue = QuantizeAxis(value);
        }

        public AxisInputEvent ReadTickInput()
        {
            return new AxisInputEvent(latestValue);
        }

        public static short QuantizeAxis(float value)
        {
            value = Math.Clamp(value, -1f, 1f);

            return (short)Math.Round(
                value * short.MaxValue
            );
        }
    }
}
