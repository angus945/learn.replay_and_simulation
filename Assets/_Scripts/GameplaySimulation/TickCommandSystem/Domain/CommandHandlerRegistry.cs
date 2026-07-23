using System;
using System.Collections.Generic;
using TickCommandSystem.Contract;

namespace TickCommandSystem.Domain
{
    internal sealed class CommandHandlerRegistry
    {
        private readonly Dictionary<Type, ICommandHandlerInvoker> handlersByCommandType = new();

        internal void RegisterHandler<TCommand>(
            ICommandHandler<TCommand> handler)
            where TCommand : ICommand
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type commandType = typeof(TCommand);
            if (handlersByCommandType.ContainsKey(commandType))
            {
                throw new InvalidOperationException(
                    $"Command handler for {commandType.Name} is already registered.");
            }

            handlersByCommandType.Add(
                commandType,
                new CommandHandlerInvoker<TCommand>(handler));
        }

        internal void Dispatch(CommandData data, ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            Type commandType = command.GetType();
            if (!handlersByCommandType.TryGetValue(
                commandType,
                out ICommandHandlerInvoker handler))
            {
                throw new InvalidOperationException(
                    $"No handler registered for command type {commandType.Name}.");
            }

            handler.Handle(data, command);
        }

        private interface ICommandHandlerInvoker
        {
            void Handle(CommandData data, ICommand command);
        }

        private sealed class CommandHandlerInvoker<TCommand> :
            ICommandHandlerInvoker
            where TCommand : ICommand
        {
            private readonly ICommandHandler<TCommand> handler;

            internal CommandHandlerInvoker(
                ICommandHandler<TCommand> handler)
            {
                this.handler = handler;
            }

            public void Handle(CommandData data, ICommand command)
            {
                handler.Handle(data, (TCommand)command);
            }
        }
    }
}
