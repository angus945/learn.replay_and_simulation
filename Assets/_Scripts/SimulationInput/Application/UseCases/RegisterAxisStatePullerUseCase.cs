using System;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class RegisterAxisStatePullerUseCase
    {
        private readonly ApplicationStats stats;

        internal RegisterAxisStatePullerUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal int Execute<TKey>(IAxisStatePuller puller) where TKey : IAxisKey
        {
            stats.EnsureCanRegister();

            if (puller == null)
                throw new ArgumentNullException(nameof(puller));

            Type keyType = typeof(TKey);
            if (stats.axisReaderIndexByKey.ContainsKey(keyType))
            {
                throw new InvalidOperationException(
                    $"Axis input key {keyType.FullName} is already registered.");
            }

            int index = stats.axisStateReader.Count;
            stats.axisReaderIndexByKey.Add(keyType, index);
            stats.axisStatePullers.Add(puller);
            stats.axisStateReader.Add(new AxisStateReader());

            return index;
        }
    }
}
