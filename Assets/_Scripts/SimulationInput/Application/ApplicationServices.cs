using SimulationInput;

namespace SimulationInput.Application
{
    internal sealed class RegisterInputCommandBuilderUseCase
    {
        private readonly ApplicationStats stats;

        internal RegisterInputCommandBuilderUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal void Execute<TCommand>(IInputCommandBuilder<TCommand> builder) where TCommand : class, IInputCommand
        {
            stats.RegisterInputCommandBuilder(builder);
        }
    }

    internal sealed class ConsumeTickCommandsUseCase
    {
        private readonly ApplicationStats stats;

        internal ConsumeTickCommandsUseCase(ApplicationStats stats)
        {
            this.stats = stats;
        }

        internal TickInputFrameCommands Execute()
        {
            stats.EnsureInitialized();
            stats.EnsureTickInputFrameConsumed();

            TickInputFrameCommands commands = stats.reusableCommands;
            commands.SetTick(stats.reusableFrame.Tick);

            for (int i = 0; i < stats.inputCommandBuilders.Count; i++)
            {
                stats.inputCommandBuilders[i].UpdateCommand(
                    stats.reusableFrame,
                    commands.Commands[i]);
            }

            return commands;
        }
    }

    public sealed class SimulationInputs
    {
        private readonly ApplicationStats stats;

        private readonly RegisterButtonStatePullerUseCase registerButtonStatePullerUseCase;
        private readonly RegisterAxisStatePullerUseCase registerAxisStatePullerUseCase;
        private readonly RegisterInputCommandBuilderUseCase registerInputCommandBuilderUseCase;
        private readonly CaptureRenderInputUseCase captureRenderInputUseCase;
        private readonly ConsumeTickInputUseCase consumeTickInputUseCase;
        private readonly ConsumeTickCommandsUseCase consumeTickCommandsUseCase;

        public SimulationInputs()
        {
            stats = new ApplicationStats();

            registerButtonStatePullerUseCase = new RegisterButtonStatePullerUseCase(stats);
            registerAxisStatePullerUseCase = new RegisterAxisStatePullerUseCase(stats);
            registerInputCommandBuilderUseCase = new RegisterInputCommandBuilderUseCase(stats);
            captureRenderInputUseCase = new CaptureRenderInputUseCase(stats);
            consumeTickInputUseCase = new ConsumeTickInputUseCase(stats);
            consumeTickCommandsUseCase = new ConsumeTickCommandsUseCase(stats);
        }

        public int RegisterButtonStatePuller<TKey>(IButtonStatePuller puller) where TKey : IButtonKey
        {
            return registerButtonStatePullerUseCase.Execute<TKey>(puller);
        }
        public int RegisterAxisStatePuller<TKey>(IAxisStatePuller puller) where TKey : IAxisKey
        {
            return registerAxisStatePullerUseCase.Execute<TKey>(puller);
        }

        public void RegisterInputCommandBuilder<TCommand>(IInputCommandBuilder<TCommand> builder)
            where TCommand : class, IInputCommand
        {
            registerInputCommandBuilderUseCase.Execute(builder);
        }

        /// <summary>
        /// Call after all state pullers and input command builders have been registered.
        /// </summary>
        public void Initialize()
        {
            stats.Initialize();
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

        public TickInputFrameCommands ConsumeTickCommands()
        {
            return consumeTickCommandsUseCase.Execute();
        }
    }
}
