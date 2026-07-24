using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.API
{
    public interface IPlayerInputSnapshot
    {
        ButtonState GetButtonState<TKey>() where TKey : IButtonInputKey;
        AxisState GetAxisState<TKey>() where TKey : IAxisInputKey;
    }
}