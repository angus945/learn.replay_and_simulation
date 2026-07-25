using SimulationCore.Contracts;

namespace SimulationCore.CommandSystem.API
{
    public interface ICommandContext
    {
        void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
        void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
        void EnqueueCommand<T>(CommandMetadata data, T command) where T : ICommand;
        void EnqueueEvent<T>(CommandMetadata data, T @event) where T : IEvent;
    }
}
