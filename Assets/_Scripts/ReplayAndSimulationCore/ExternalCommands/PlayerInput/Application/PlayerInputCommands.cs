using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.Contract;
using SimulationCore.ExternalCommands.Port;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    public sealed class PlayerInputCommands : IExternalCommandProvider
    {
        readonly InputStats inputStats;

        readonly RegisterButtonStatePullerUseCase registerButtonStatePullerUseCase;
        readonly RegisterAxisStatePullerUseCase registerAxisStatePullerUseCase;
        readonly RegisterInputCommandUseCase registerInputCommandUseCase;

        readonly CaptureRenderInputUseCase captureRenderInputUseCase;
        readonly ProduceInputCommandUseCase produceInputCommandUseCase;

        public PlayerInputCommands(ICommandEnqueuePort commandPort, IButtonRegistrationPort buttonPort, IAxisRegistrationPort axisPort, IRuleRegistrationPort rulePort)
        {
            inputStats = new InputStats();

            registerButtonStatePullerUseCase = new RegisterButtonStatePullerUseCase(inputStats, buttonPort);
            registerAxisStatePullerUseCase = new RegisterAxisStatePullerUseCase(inputStats, axisPort);
            registerInputCommandUseCase = new RegisterInputCommandUseCase(rulePort);

            captureRenderInputUseCase = new CaptureRenderInputUseCase(inputStats, buttonPort, axisPort);
            produceInputCommandUseCase = new ProduceInputCommandUseCase(inputStats, commandPort, rulePort);
        }

        public int RegisterButtonStatePuller<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey
        {
            return registerButtonStatePullerUseCase.Execute<TKey>(puller);
        }
        public int RegisterAxisStatePuller<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey
        {
            return registerAxisStatePullerUseCase.Execute<TKey>(puller);
        }
        public void RegisterInputCommand<TCommand>(IInputCommandRule commandRule) where TCommand : struct, ICommand
        {
            registerInputCommandUseCase.Execute<TCommand>(commandRule);
        }

        /// <summary>
        /// Call after all state pullers have been registered.
        /// </summary>
        public void Initialize()
        {
            inputStats.Initialize();
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
        public void EnqueueCommands(ulong tick)
        {
            produceInputCommandUseCase.Execute(tick);
        }
    }
}
