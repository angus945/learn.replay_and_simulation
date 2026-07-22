using System;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class RegisterAxisStatePullerUseCase
    {
        private readonly ButtonReaderStats stats;

        internal RegisterAxisStatePullerUseCase(ButtonReaderStats stats)
        {
            this.stats = stats;
        }

        internal int Execute(IAxisStatePuller puller)
        {
            if (puller == null)
                throw new ArgumentNullException(nameof(puller));

            int index = stats.axisStateReader.Count;
            stats.axisStatePullers.Add(puller);
            stats.axisStateReader.Add(new AxisStateReader());

            return index;
        }
    }
}
