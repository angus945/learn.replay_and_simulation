using System;
using System.Collections.Generic;

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
    }

    public readonly struct AxisInputEvent
    {
        public readonly float Value;

        public AxisInputEvent(float value)
        {
            Value = value;
        }
    }
}
