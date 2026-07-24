using SimulationCore.Contracts;

namespace SimulationCore.ExternalCommands.Port
{
    public interface ICommandEnqueuePort
    {
        void EnqueueCommands(CommandMetadata commandData, ICommand commandQueue);
    }
}