using System.Globalization;
using System.Text;
using SimulationCore.Logging.API;
using SimulationCore.Logging.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class CaptureRenderInputUseCase
    {
        InputStats stats;

        IButtonRegistrationPort buttonPort;
        IAxisRegistrationPort axisPort;

        internal CaptureRenderInputUseCase(InputStats stats, IButtonRegistrationPort buttonPort, IAxisRegistrationPort axisPort)
        {
            this.stats = stats;
            this.buttonPort = buttonPort;
            this.axisPort = axisPort;
        }

        internal void Execute()
        {
            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                bool isPressed = buttonPort.PullButtonStat(i);
                stats.CaptureRawButtonState(i, isPressed);
            }

            for (int i = 0; i < stats.axisStateReader.Count; i++)
            {
                float value = axisPort.PullAxisStat(i);
                stats.CaptureRawAxisState(i, value);
            }
        }
    }
}
