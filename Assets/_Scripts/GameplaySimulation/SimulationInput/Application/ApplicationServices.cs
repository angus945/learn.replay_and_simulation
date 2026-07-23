using Logging.API;
using Logging.Contract;
using Logging.Infrastructure;
using SimulationInput;
using SimulationInput.API;
using SimulationInput.Contract;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    public class FrameSnapShot : IInputSnapshot
    {
        private readonly TickInputFrame frame;

        public FrameSnapShot(TickInputFrame frame)
        {
            this.frame = frame;
        }

        public ButtonState GetButtonState<TKey>() where TKey : IButtonInputKey
        {
            var button = frame.GetButton<TKey>();
            return new ButtonState(button.IsPressed, button.IsDown, button.IsReleased);
        }

        public AxisState GetAxisState<TKey>() where TKey : IAxisInputKey
        {
            var axis = frame.GetAxis<TKey>();
            return new AxisState(axis.Value);
        }
    }
    public sealed class SimulationInputs : ISimulationInputRuntime
    {
        private readonly ApplicationStats stats;
        private readonly ILogger logger;

        private readonly RegisterButtonStatePullerUseCase registerButtonStatePullerUseCase;
        private readonly RegisterAxisStatePullerUseCase registerAxisStatePullerUseCase;
        private readonly CaptureRenderInputUseCase captureRenderInputUseCase;
        private readonly ConsumeTickInputUseCase consumeTickInputUseCase;

        public SimulationInputs() : this(NullLogger.Instance)
        {

        }

        public SimulationInputs(ILogger logger)
        {
            this.logger = logger ?? NullLogger.Instance;
            stats = new ApplicationStats(this.logger);

            registerButtonStatePullerUseCase = new RegisterButtonStatePullerUseCase(stats);
            registerAxisStatePullerUseCase = new RegisterAxisStatePullerUseCase(stats);
            captureRenderInputUseCase = new CaptureRenderInputUseCase(stats);
            consumeTickInputUseCase = new ConsumeTickInputUseCase(stats);
        }

        public int RegisterButtonStatePuller<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey
        {
            return registerButtonStatePullerUseCase.Execute<TKey>(puller);
        }
        public int RegisterAxisStatePuller<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey
        {
            return registerAxisStatePullerUseCase.Execute<TKey>(puller);
        }


        /// <summary>
        /// Call after all state pullers have been registered.
        /// </summary>
        public void Initialize()
        {
            stats.Initialize();
        }

        /// <summary>
        /// 每個 Render Frame 呼叫一次。
        /// </summary>
        public void CaptureRenderInput()
        {
            captureRenderInputUseCase.Execute();
        }

        /// <summary>
        /// 每個新的 Simulation Tick 呼叫一次。
        /// </summary>
        public IInputSnapshot ConsumeSnapshot(ulong tick)
        {
            consumeTickInputUseCase.Execute(tick);

            if (logger.IsEnabled(LogLevel.Trace))
            {
                string lastTick = stats.hasCommittedTick
                    ? stats.lastCommittedTick.ToString()
                    : "none";

                logger.Trace(
                    $"Snapshot requested. Tick: {tick}, last committed tick: {lastTick}.",
                    ApplicationStats.LogCategory);
            }

            return stats.snapshot;
        }

    }
}
