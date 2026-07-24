using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    public class FrameSnapShot : IPlayerInputSnapshot
    {
        private readonly TickInputFrame frame;

        public FrameSnapShot(TickInputFrame frame)
        {
            this.frame = frame;
        }

        public ButtonState GetButtonState<TKey>() where TKey : IButtonInputKey
        {
            var button = frame.GetButton<TKey>();
            return new ButtonState(button.IsPressed, button.IsDown, button.IsReleased);
        }

        public AxisState GetAxisState<TKey>() where TKey : IAxisInputKey
        {
            var axis = frame.GetAxis<TKey>();
            return new AxisState(axis.Value);
        }
    }
}
