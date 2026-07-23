using System;
using SimulationInput.API;
using SimulationInput.Contract;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class RegisterButtonStatePullerUseCase
    {
        private readonly ApplicationStats stats;

        internal RegisterButtonStatePullerUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal int Execute<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey
        {
            stats.EnsureCanRegister();

            if (puller == null)
                throw new ArgumentNullException(nameof(puller));

            Type keyType = typeof(TKey);
            if (stats.buttonReaderIndexByKey.ContainsKey(keyType))
            {
                throw new InvalidOperationException(
                    $"Button input key {keyType.FullName} is already registered.");
            }

            int index = stats.buttonStateReader.Count;
            stats.buttonReaderIndexByKey.Add(keyType, index);
            stats.buttonStatePullers.Add(puller);
            stats.buttonStateReader.Add(new ButtonStateReader());

            return index;
        }
    }
}
