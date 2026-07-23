using System;
using Logging.API;
using SimulationInput.Domain;
using SimulationInput.Contract;

namespace SimulationInput.Application
{
    internal sealed class RegisterAxisStatePullerUseCase
    {
        private readonly ApplicationStats stats;

        internal RegisterAxisStatePullerUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal int Execute<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey
        {
            stats.EnsureCanRegister();

            Type keyType = typeof(TKey);

            if (puller == null)
            {
                stats.logger.Warning(
                    $"Axis input puller registration rejected. Key: {keyType.FullName}, reason: null puller.",
                    ApplicationStats.LogCategory);

                throw new ArgumentNullException(nameof(puller));
            }

            if (stats.axisReaderIndexByKey.ContainsKey(keyType))
            {
                stats.logger.Warning(
                    $"Axis input puller registration rejected. Key: {keyType.FullName}, reason: duplicate key.",
                    ApplicationStats.LogCategory);

                throw new InvalidOperationException(
                    $"Axis input key {keyType.FullName} is already registered.");
            }

            int index = stats.axisStateReader.Count;
            stats.axisReaderIndexByKey.Add(keyType, index);
            stats.axisKeyTypes.Add(keyType);
            stats.axisStatePullers.Add(puller);
            stats.axisStateReader.Add(new AxisStateReader());

            stats.logger.Info(
                $"Registered axis input puller. Key: {keyType.FullName}, index: {index}.",
                ApplicationStats.LogCategory);

            return index;
        }
    }
}
