using System;
using System.Collections.Generic;
using SimulationCore.Contracts;

namespace SimulationCore.CommandSystem.Domain
{
    internal sealed class CommandHandlerRegistry
    {
        private readonly Dictionary<Type, ICommandHandlerInvoker> handlersByCommandType = new();
        private readonly Dictionary<Type, List<IEventHandlerInvoker>> handlersByEventType = new();

        internal void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type commandType = typeof(TCommand);
            if (handlersByCommandType.ContainsKey(commandType))
            {
                throw new InvalidOperationException(
                    $"Command handler for {commandType.Name} is already registered.");
            }

            handlersByCommandType.Add(commandType, new CommandHandlerInvoker<TCommand>(handler));
        }
        internal void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type eventType = typeof(TEvent);
            if (!handlersByEventType.TryGetValue(eventType, out List<IEventHandlerInvoker> handlers))
            {
                handlers = new List<IEventHandlerInvoker>();
                handlersByEventType.Add(eventType, handlers);
            }

            handlers.Add(new EventHandlerInvoker<TEvent>(handler, handlers.Count));
        }

        internal void DispatchCommand(CommandMetadata data, ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            Type commandType = command.GetType();
            if (!handlersByCommandType.TryGetValue(commandType, out ICommandHandlerInvoker handler))
            {
                throw new InvalidOperationException(
                    $"No handler registered for command type {commandType.Name}.");
            }

            handler.Handle(data, command);
        }
        internal void DispatchEvent(CommandMetadata data, IEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            Type eventType = @event.GetType();
            if (handlersByEventType.TryGetValue(eventType, out List<IEventHandlerInvoker> handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.Handle(data, @event);
                }
            }
        }

        private interface ICommandHandlerInvoker
        {
            void Handle(CommandMetadata data, ICommand command);
        }
        private sealed class CommandHandlerInvoker<TCommand> : ICommandHandlerInvoker where TCommand : ICommand
        {
            public readonly string handlerName;
            private readonly ICommandHandler<TCommand> handler;

            internal CommandHandlerInvoker(ICommandHandler<TCommand> handler)
            {
                this.handler = handler;
                handlerName = handler.GetType().Name;
            }

            public void Handle(CommandMetadata data, ICommand command)
            {
                handler.Handle((TCommand)command);
            }
        }

        private interface IEventHandlerInvoker
        {
            void Handle(CommandMetadata data, IEvent @event);
        }
        private sealed class EventHandlerInvoker<TEvent> : IEventHandlerInvoker where TEvent : IEvent
        {
            public readonly string handlerName;
            public readonly int handlerOrder;
            private readonly IEventHandler<TEvent> handler;

            internal EventHandlerInvoker(IEventHandler<TEvent> handler, int order)
            {
                this.handler = handler;
                this.handlerOrder = order;
            }

            public void Handle(CommandMetadata data, IEvent @event)
            {
                handler.Handle((TEvent)@event);
            }
        }
    }
}
