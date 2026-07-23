using System.Globalization;
using System.Text;
using Logging.API;
using Logging.Contract;

namespace SimulationInput.Application
{
    internal sealed class CaptureRenderInputUseCase
    {
        private readonly ApplicationStats stats;

        internal CaptureRenderInputUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal void Execute()
        {
            stats.EnsureInitialized();

            bool shouldLog = stats.logger.IsEnabled(LogLevel.Trace);
            StringBuilder logBuilder = null;

            if (shouldLog)
            {
                logBuilder = new StringBuilder("Captured render input. Buttons: ");
                if (stats.buttonStateReader.Count == 0)
                    logBuilder.Append("none");
            }

            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                bool isPressed = stats.buttonStatePullers[i].IsPressed;
                stats.buttonStateReader[i].CaptureRawState(isPressed);

                if (shouldLog)
                {
                    if (i > 0)
                        logBuilder.Append(", ");

                    logBuilder
                        .Append(stats.GetButtonKeyName(i))
                        .Append(".isPressed=")
                        .Append(isPressed);
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
                float value = stats.axisStatePullers[i].Value;
                stats.axisStateReader[i].CaptureRawState(value);

                if (shouldLog)
                {
                    if (i > 0)
                        logBuilder.Append(", ");

                    logBuilder
                        .Append(stats.GetAxisKeyName(i))
                        .Append(".raw=")
                        .Append(value.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }

            if (shouldLog)
            {
                stats.logger.Trace(
                    logBuilder.ToString(),
                    ApplicationStats.LogCategory);
            }
        }
    }
}
