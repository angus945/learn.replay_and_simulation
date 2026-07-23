using SimulationInput.API;
using TickCommandSystem.API;
using TickCommandSystem.Contract;
using TickIntentsBuilder.API;
using TickIntentsBuilder.Contract;

namespace TickIntentsBuilder.Application
{
    public sealed class TickIntentsBuilder : IInputCommandProducer
    {
        readonly TickIntentsBuilderStats stats;
        readonly RegisterInputCommandUseCase registerInputCommandUseCase;
        readonly ProduceInputCommandsUseCase produceInputCommandsUseCase;
        readonly CommitTickUseCase commitTickUseCase;
        readonly EnqueueCommittedCommandsUseCase enqueueCommittedCommandsUseCase;

        public TickIntentsBuilder()
        {
            stats = new TickIntentsBuilderStats();

            registerInputCommandUseCase = new RegisterInputCommandUseCase(stats);
            produceInputCommandsUseCase = new ProduceInputCommandsUseCase(stats);
            commitTickUseCase = new CommitTickUseCase(stats);
            enqueueCommittedCommandsUseCase = new EnqueueCommittedCommandsUseCase(stats);
        }

        public void RegisterInputCommand<TCommand>(
            IInputCommandRule commandRule)
            where TCommand : struct, ICommand
        {
            registerInputCommandUseCase.Execute<TCommand>(commandRule);
        }

        public void ProduceInputCommands(IInputSnapshot snapshot)
        {
            produceInputCommandsUseCase.Execute(snapshot);
        }

        public void CommitTick(ulong tick)
        {
            commitTickUseCase.Execute(tick);
        }

        public void EnqueueCommittedCommands(
            ITickCommandQueue commandQueue,
            CommandType type = CommandType.Gameplay)
        {
            enqueueCommittedCommandsUseCase.Execute(commandQueue, type);
        }
    }
}
