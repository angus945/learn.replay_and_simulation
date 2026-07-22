using System;
using SimulationInput;

namespace SimulationInput.Application
{
    internal sealed class ConsumeTickInputUseCase
    {
        private readonly ApplicationStats stats;

        internal ConsumeTickInputUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal TickInputFrame Execute(ConsumeInputCommand command)
        {
            stats.EnsureInitialized();

            ulong tick = command.tick;

            if (stats.hasCommittedTick && tick <= stats.lastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"Input tick must increase monotonically. " +
                    $"Last tick: {stats.lastCommittedTick}, requested tick: {tick}."
                );
            }

            TickInputFrame frame = stats.reusableFrame;
            frame.SetTick(tick);

            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                frame.Buttons[i] = stats.buttonStateReader[i].ConsumeTickInput();
            }

            for (int i = 0; i < stats.axisStateReader.Count; i++)
            {
                frame.Axes[i] = stats.axisStateReader[i].ReadTickInput();
            }

            stats.hasCommittedTick = true;
            stats.lastCommittedTick = tick;

            return frame;
        }
    }
}
