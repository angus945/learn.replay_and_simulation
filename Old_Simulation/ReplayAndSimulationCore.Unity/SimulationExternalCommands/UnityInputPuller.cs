using SimulationCore.ExternalCommands.PlayerInput.Application;
using UnityEngine;

namespace SimulationCore.ExternalCommands.PlayerInput.Infrastructure
{
    public sealed class UnityAxisStatePuller : IAxisStatePuller
    {
        private readonly string _axisName;

        public UnityAxisStatePuller(string axisName)
        {
            _axisName = axisName;
        }

        public float Value => Input.GetAxisRaw(_axisName);
    }
    public sealed class UnityButtonStatePuller : IButtonStatePuller
    {
        private readonly string _buttonName;

        public UnityButtonStatePuller(string buttonName)
        {
            _buttonName = buttonName;
        }

        public bool IsPressed => Input.GetButton(_buttonName);
    }
}