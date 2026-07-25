using System;
using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.World.Application;

namespace SimulationCore.World.Infrastructure
{
    public sealed class CommandSubscriberPort : ICommandHandleRegistryPort
    {
        private readonly ICommandContext commandContext;

        public CommandSubscriberPort(ICommandContext commandContext)
        {
            this.commandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
        }
        public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            commandContext.RegisterCommandHandler(handler);
        }
    }
}
