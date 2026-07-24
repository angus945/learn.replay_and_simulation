using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.Port;
using SimulationCore.Logging.API;

namespace SimulationCore.ExternalCommands.PlayerInput.Infrastructure
{
    public class CommandEnqueuePort : ICommandEnqueuePort
    {
        readonly ICommandContext commandContext;
        readonly ILogger logger;

        public CommandEnqueuePort(ICommandContext commandContext, ILogger logger)
        {
            this.commandContext = commandContext;
            this.logger = logger;
        }

        public void EnqueueCommands(CommandMetadata commandData, ICommand command)
        {
            logger.Trace($"Enqueueing command: {command.GetType().Name} with meta: {commandData}, payload: {command.ToString()}");

            commandContext.EnqueueCommand(commandData, command);
        }
    }
}