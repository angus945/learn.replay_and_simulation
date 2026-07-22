namespace SimulationInput
{
    public sealed class TickInputFrame
    {
        public ulong Tick { get; }
        public ButtonInputEvent[] Buttons { get; }
        public AxisInputEvent[] Axes { get; }

        public TickInputFrame(ulong tick, ButtonInputEvent[] buttons, AxisInputEvent[] axes)
        {
            Tick = tick;
            Buttons = buttons;
            Axes = axes;
        }
    }

    public readonly struct ButtonInputEvent
    {
        public readonly bool IsPressed;
        public readonly bool IsDown;
        public readonly bool IsReleased;

        public ButtonInputEvent(bool isPressed, bool isDown, bool isReleased)
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
