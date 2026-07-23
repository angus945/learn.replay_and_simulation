using SimulationInput.Contract;


namespace SimulationInput.Contract
{
    public interface IButtonInputKey { }
    public interface IAxisInputKey { }
    public readonly struct ButtonState
    {
        public readonly bool IsPressed;
        public readonly bool IsDown;
        public readonly bool IsReleased;

        public ButtonState(bool isPressed, bool isDown, bool isReleased)
        {
            IsPressed = isPressed;
            IsDown = isDown;
            IsReleased = isReleased;
        }
    }
    public readonly struct AxisState
    {
        public readonly float Value;

        public AxisState(float value)
        {
            Value = value;
        }
    }
}
