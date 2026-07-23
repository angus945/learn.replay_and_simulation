using System;
using Logging.API;
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

            Type keyType = typeof(TKey);

            if (puller == null)
            {
                stats.logger.Warning(
                    $"Button input puller registration rejected. Key: {keyType.FullName}, reason: null puller.",
                    ApplicationStats.LogCategory);

                throw new ArgumentNullException(nameof(puller));
            }

            if (stats.buttonReaderIndexByKey.ContainsKey(keyType))
            {
                stats.logger.Warning(
                    $"Button input puller registration rejected. Key: {keyType.FullName}, reason: duplicate key.",
                    ApplicationStats.LogCategory);

                throw new InvalidOperationException(
                    $"Button input key {keyType.FullName} is already registered.");
            }

            int index = stats.buttonStateReader.Count;
            stats.buttonReaderIndexByKey.Add(keyType, index);
            stats.buttonKeyTypes.Add(keyType);
            stats.buttonStatePullers.Add(puller);
            stats.buttonStateReader.Add(new ButtonStateReader());

            stats.logger.Info(
                $"Registered button input puller. Key: {keyType.FullName}, index: {index}.",
                ApplicationStats.LogCategory);

            return index;
        }
    }
}
