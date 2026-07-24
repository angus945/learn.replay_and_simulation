using System;
using SimulationCore.Logging.API;
using SimulationCore.ExternalCommands.PlayerInput.Domain;
using SimulationCore.ExternalCommands.PlayerInput.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class RegisterAxisStatePullerUseCase
    {
        private readonly InputStats inputStats;
        private readonly IAxisRegistrationPort axisPort;

        internal RegisterAxisStatePullerUseCase(InputStats inputStats, IAxisRegistrationPort axisPort)
        {
            this.inputStats = inputStats ?? throw new ArgumentNullException(nameof(inputStats));
            this.axisPort = axisPort ?? throw new ArgumentNullException(nameof(axisPort));
        }

        internal int Execute<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey
        {
            Type keyType = typeof(TKey);

            if (puller == null)
            {
                throw new ArgumentNullException(nameof(puller));
            }

            if (axisPort.IsKeyRegistered<TKey>())
            {
                throw new InvalidOperationException(
                    $"Axis input key {keyType.FullName} is already registered.");
            }

            int index = axisPort.RegisterAxisStatePuller<TKey>(puller);
            inputStats.AddAxisStateReader(keyType, index);

            return index;
        }
    }
}
