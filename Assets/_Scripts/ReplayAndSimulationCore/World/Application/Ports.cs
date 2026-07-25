using SimulationCore.Contracts;

namespace SimulationCore.World.Application
{
    public interface ICommandHandleRegistryPort
    {
        void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
    }
}