using SimulationCore.Contracts;

namespace SimulationCore.CommandSystem.API
{
    public interface ICommandContext
    {
        void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
        void EnqueueCommand(CommandMetadata data, ICommand commandInstance);
    }
}
