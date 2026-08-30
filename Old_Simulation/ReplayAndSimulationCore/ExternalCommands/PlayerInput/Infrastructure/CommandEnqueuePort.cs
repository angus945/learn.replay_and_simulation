using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.Port;
using SimulationCore.Logging.API;

namespace SimulationCore.ExternalCommands.PlayerInput.Infrastructure
{
    public class CommandEnqueuePort : ICommandPort
    {
        readonly ICommandContext commandContext;
        readonly ILogger logger;

        public CommandEnqueuePort(ICommandContext commandContext, ILogger logger)
        {
            this.commandContext = commandContext;
            this.logger = logger;
        }

        public void EnqueueCommand<T>(CommandMetadata commandData, T command) where T : ICommand
        {
            logger.Trace($"Enqueueing command: {command.GetType().Name} with meta: {commandData}, payload: {command.ToString()}");

            commandContext.EnqueueCommand(commandData, command);
        }
        public void EnqueueEvent<T>(CommandMetadata eventData, T @event) where T : IEvent
        {
            logger.Trace($"Enqueueing event: {@event.GetType().Name} with meta: {eventData}, payload: {@event.ToString()}");

            commandContext.EnqueueEvent(eventData, @event);
        }
    }
}