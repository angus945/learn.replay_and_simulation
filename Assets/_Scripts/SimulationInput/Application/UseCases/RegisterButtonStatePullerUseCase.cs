using System;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class RegisterButtonStatePullerUseCase
    {
        private readonly ButtonReaderStats stats;

        internal RegisterButtonStatePullerUseCase(ButtonReaderStats stats)
        {
            this.stats = stats;
        }

        internal int Execute(IButtonStatePuller puller)
        {
            if (puller == null)
                throw new ArgumentNullException(nameof(puller));

            int index = stats.buttonStateReader.Count;
            stats.buttonStatePullers.Add(puller);
            stats.buttonStateReader.Add(new ButtonStateReader());

            return index;
        }
    }
}
