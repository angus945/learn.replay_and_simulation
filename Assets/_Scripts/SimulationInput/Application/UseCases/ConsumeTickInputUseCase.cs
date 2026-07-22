using System;
using SimulationInput;

namespace SimulationInput.Application
{
    internal sealed class ConsumeTickInputUseCase
    {
        private readonly ButtonReaderStats stats;

        internal ConsumeTickInputUseCase(ButtonReaderStats stats)
        {
            this.stats = stats;
        }

        internal TickInputFrame Execute(ConsumeInputCommand command)
        {
            ulong tick = command.tick;

            if (stats.hasCommittedTick && tick <= stats.lastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"Input tick must increase monotonically. " +
                    $"Last tick: {stats.lastCommittedTick}, requested tick: {tick}."
                );
            }

            ButtonInputEvent[] buttonInputs = new ButtonInputEvent[stats.buttonStateReader.Count];

            AxisInputEvent[] axisInputs = new AxisInputEvent[stats.axisStateReader.Count];

            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                buttonInputs[i] = stats.buttonStateReader[i].ConsumeTickInput();
            }

            for (int i = 0; i < stats.axisStateReader.Count; i++)
            {
                axisInputs[i] = stats.axisStateReader[i].ReadTickInput();
            }

            TickInputFrame frame = new TickInputFrame(
                tick,
                buttonInputs,
                axisInputs
            );

            stats.hasCommittedTick = true;
            stats.lastCommittedTick = tick;

            return frame;
        }
    }
}
