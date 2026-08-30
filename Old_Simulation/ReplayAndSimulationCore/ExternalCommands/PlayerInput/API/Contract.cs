using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands.PlayerInput.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.Contract
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

    public interface IInputCommandRule
    {
        bool TryProduce(IPlayerInputSnapshot snapshot, out ICommand command);
    }
}
