namespace SimulationInput.Application
{
    internal sealed class CaptureRenderInputUseCase
    {
        private readonly ButtonReaderStats stats;

        internal CaptureRenderInputUseCase(ButtonReaderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(CaptureInputCommand command)
        {
            for (int i = 0; i < stats.buttonStateReader.Count; i++)
            {
                bool isPressed = stats.buttonStatePullers[i].IsPressed;
                stats.buttonStateReader[i].CaptureRawState(isPressed);
            }

            for (int i = 0; i < stats.axisStateReader.Count; i++)
            {
                float value = stats.axisStatePullers[i].Value;
                stats.axisStateReader[i].CaptureRawState(value);
            }
        }
    }
}
