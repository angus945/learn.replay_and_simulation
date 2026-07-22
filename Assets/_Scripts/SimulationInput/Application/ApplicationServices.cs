using SimulationInput;

namespace SimulationInput.Application
{
    public sealed class ApplicationServices
    {
        private readonly ButtonReaderStats buttonReaderStats;

        private readonly RegisterButtonStatePullerUseCase registerButtonStatePullerUseCase;
        private readonly RegisterAxisStatePullerUseCase registerAxisStatePullerUseCase;
        private readonly CaptureRenderInputUseCase captureRenderInputUseCase;
        private readonly ConsumeTickInputUseCase consumeTickInputUseCase;

        public ApplicationServices()
        {
            buttonReaderStats = new ButtonReaderStats();

            registerButtonStatePullerUseCase = new RegisterButtonStatePullerUseCase(buttonReaderStats);
            registerAxisStatePullerUseCase = new RegisterAxisStatePullerUseCase(buttonReaderStats);
            captureRenderInputUseCase = new CaptureRenderInputUseCase(buttonReaderStats);
            consumeTickInputUseCase = new ConsumeTickInputUseCase(buttonReaderStats);
        }

        public int RegisterButtonStatePuller(IButtonStatePuller puller)
        {
            return registerButtonStatePullerUseCase.Execute(puller);
        }
        public int RegisterAxisStatePuller(IAxisStatePuller puller)
        {
            return registerAxisStatePullerUseCase.Execute(puller);
        }

        /// <summary>
        /// 每個 Render Frame 呼叫一次。
        /// </summary>
        public void CaptureRenderInput(CaptureInputCommand command)
        {
            captureRenderInputUseCase.Execute(command);
        }

        /// <summary>
        /// 每個新的 Simulation Tick 呼叫一次。
        /// </summary>
        public TickInputFrame ConsumeTick(ConsumeInputCommand command)
        {
            return consumeTickInputUseCase.Execute(command);
        }
    }
}
