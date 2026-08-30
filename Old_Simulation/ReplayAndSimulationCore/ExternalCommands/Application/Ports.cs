using SimulationCore.Contracts;

namespace SimulationCore.ExternalCommands.Port
{
    public interface ICommandPort
    {
        void EnqueueCommand<T>(CommandMetadata commandData, T command) where T : ICommand;
        void EnqueueEvent<T>(CommandMetadata eventData, T @event) where T : IEvent;
    }
}