using System;
using System.Text;
using Logging.API;
using Logging.Contract;
using SimulationInput;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class ConsumeTickInputUseCase
    {
        private readonly ApplicationStats stats;

        internal ConsumeTickInputUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal TickInputFrame Execute(ulong tick)
        {
            stats.EnsureInitialized();

            if (stats.hasCommittedTick && tick <= stats.lastCommittedTick)
            {
                stats.logger.Warning(
                    $"Tick input consume rejected. Last tick: {stats.lastCommittedTick}, requested tick: {tick}.",
                    ApplicationStats.LogCategory);

                throw new InvalidOperationException(
                    $"Input tick must increase monotonically. " +
                    $"Last tick: {stats.lastCommittedTick}, requested tick: {tick}."
                );
            }

            TickInputFrame frame = stats.reusableFrame;
            frame.SetTick(tick);

            bool shouldLog = stats.logger.IsEnabled(LogLevel.Debug);
            StringBuilder logBuilder = null;

            if (shouldLog)
            {
                logBuilder = new StringBuilder();
                logBuilder
                    .Append("Consumed tick input. Tick: ")
                    .Append(tick)
                    .Append(", buttons: ");

                if (stats.buttonStateReader.Count == 0)
                    logBuilder.Append("none");
            }

            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                frame.Buttons[i] = stats.buttonStateReader[i].ConsumeTickInput();

                if (shouldLog)
                {
                    if (i > 0)
                        logBuilder.Append(", ");

                    logBuilder
                        .Append(stats.GetButtonKeyName(i))
                        .Append("(")
                        .Append(frame.Buttons[i].ToString())
                        .Append(")");
                }
            }

            if (shouldLog)
            {
                logBuilder.Append("; axes: ");
                if (stats.axisStateReader.Count == 0)
                    logBuilder.Append("none");
            }

            for (int i = 0; i < stats.axisStateReader.Count; i++)
            {
                frame.Axes[i] = stats.axisStateReader[i].ReadTickInput();

                if (shouldLog)
                {
                    if (i > 0)
                        logBuilder.Append(", ");

                    logBuilder
                        .Append(stats.GetAxisKeyName(i))
                        .Append("(")
                        .Append(frame.Axes[i].ToString())
                        .Append(")");
                }
            }

            stats.hasCommittedTick = true;
            stats.lastCommittedTick = tick;

            if (shouldLog)
            {
                stats.logger.Debug(
                    logBuilder.ToString(),
                    ApplicationStats.LogCategory);
            }

            return frame;
        }
    }
}
