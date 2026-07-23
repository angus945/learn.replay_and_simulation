using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimulationInput.Domain
{
    public readonly struct ButtonInputState
    {
        public readonly bool IsPressed;
        public readonly bool IsDown;
        public readonly bool IsReleased;

        public ButtonInputState(bool isPressed, bool isDown, bool isReleased)
        {
            IsPressed = isPressed;
            IsDown = isDown;
            IsReleased = isReleased;
        }

        public override string ToString()
        {
            return $"pressed={IsPressed}, down={IsDown}, released={IsReleased}";
        }
    }

    public readonly struct AxisInputEvent
    {
        public readonly float Value;

        public AxisInputEvent(float value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"value={Value.ToString("0.###", CultureInfo.InvariantCulture)}";
        }
    }
}
