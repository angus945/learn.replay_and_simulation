using System;
using SimulationCore.Logging.API;
using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class RegisterButtonStatePullerUseCase
    {
        private readonly InputStats inputStats;
        private readonly IButtonRegistrationPort buttonPort;

        internal RegisterButtonStatePullerUseCase(InputStats inputStats, IButtonRegistrationPort registrationPort)
        {
            this.inputStats = inputStats ?? throw new ArgumentNullException(nameof(inputStats));
            this.buttonPort = registrationPort ?? throw new ArgumentNullException(nameof(registrationPort));
        }

        internal int Execute<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey
        {
            Type keyType = typeof(TKey);

            if (puller == null)
            {
                throw new ArgumentNullException(nameof(puller));
            }

            if (buttonPort.IsKeyRegistered<TKey>())
            {
                throw new InvalidOperationException($"Button input key {keyType.FullName} is already registered.");
            }

            int index = buttonPort.RegisterButtonStatePuller<TKey>(puller);
            inputStats.AddButtonStateReader(keyType, index);

            return index;
        }
    }
}
