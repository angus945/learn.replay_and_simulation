using TickCommandSystem.Contract;

namespace TickCommandSystem.API
{
    public interface ITickCommandQueue
    {
        void EnqueueCommand(CommandData data, ICommand commandInstance);
    }

    public interface ITickCommandDispatcher : ITickCommandQueue
    {
        int MaxCommandWaves { get; }

        void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;

        int DispatchCommands();
    }
}
