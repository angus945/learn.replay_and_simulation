using System;
using System.Collections.Generic;
using DeterministicSimulation;
using WaveDispatching;

namespace DeterministicSimulation.Framework
{
    internal readonly struct MessageEnvelope
    {
        internal MessageEnvelope(Type messageType, object message)
        {
            MessageType = messageType;
            Message = message;
        }

        internal Type MessageType { get; }
        internal object Message { get; }
    }

    internal interface IMessageHandlerInvoker
    {
        void Invoke(object message);
    }

    internal sealed class IntentHandlerInvoker<TIntent> : IMessageHandlerInvoker
        where TIntent : IIntent
    {
        private readonly IIntentHandler<TIntent> handler;

        internal IntentHandlerInvoker(IIntentHandler<TIntent> handler)
        {
            this.handler = handler;
        }

        public void Invoke(object message)
        {
            handler.Handle((TIntent)message);
        }
    }

    internal sealed class InternalCommandHandlerInvoker<TCommand> : IMessageHandlerInvoker
        where TCommand : IInternalCommand
    {
        private readonly IInternalCommandHandler<TCommand> handler;

        internal InternalCommandHandlerInvoker(IInternalCommandHandler<TCommand> handler)
        {
            this.handler = handler;
        }

        public void Invoke(object message)
        {
            handler.Handle((TCommand)message);
        }
    }

    internal sealed class DomainEventHandlerInvoker<TEvent> : IMessageHandlerInvoker
        where TEvent : IDomainEvent
    {
        private readonly IDomainEventHandler<TEvent> handler;

        internal DomainEventHandlerInvoker(IDomainEventHandler<TEvent> handler)
        {
            this.handler = handler;
        }

        public void Invoke(object message)
        {
            handler.Handle((TEvent)message);
        }
    }

    internal sealed class MessagePipeline
    {
        private readonly Dictionary<Type, IMessageHandlerInvoker> intentHandlers =
            new Dictionary<Type, IMessageHandlerInvoker>();

        private readonly Dictionary<Type, IMessageHandlerInvoker> internalCommandHandlers =
            new Dictionary<Type, IMessageHandlerInvoker>();

        private readonly Dictionary<Type, List<IMessageHandlerInvoker>> domainEventHandlers =
            new Dictionary<Type, List<IMessageHandlerInvoker>>();

        private readonly WaveDispatcher<MessageEnvelope> intents;
        private readonly WaveDispatcher<MessageEnvelope> internalCommands;
        private readonly WaveDispatcher<MessageEnvelope> domainEvents;
        private readonly int maxReactionCycles;
        private readonly Action<MessageDispatch> onDispatch;

        internal MessagePipeline(int maxWaves, int maxReactionCycles, Action<MessageDispatch> onDispatch)
        {
            if (maxReactionCycles <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReactionCycles));
            }

            intents = new WaveDispatcher<MessageEnvelope>(maxWaves);
            internalCommands = new WaveDispatcher<MessageEnvelope>(maxWaves);
            domainEvents = new WaveDispatcher<MessageEnvelope>(maxWaves);
            this.maxReactionCycles = maxReactionCycles;
            this.onDispatch = onDispatch;
        }

        internal bool HasReactions => internalCommands.HasPending || domainEvents.HasPending;

        internal void RegisterIntentHandler<TIntent>(IIntentHandler<TIntent> handler)
            where TIntent : IIntent
        {
            AddSingleHandler(
                intentHandlers,
                typeof(TIntent),
                new IntentHandlerInvoker<TIntent>(RequireHandler(handler)),
                "intent");
        }

        internal void RegisterInternalCommandHandler<TCommand>(IInternalCommandHandler<TCommand> handler)
            where TCommand : IInternalCommand
        {
            AddSingleHandler(
                internalCommandHandlers,
                typeof(TCommand),
                new InternalCommandHandlerInvoker<TCommand>(RequireHandler(handler)),
                "internal command");
        }

        internal void RegisterDomainEventHandler<TEvent>(IDomainEventHandler<TEvent> handler)
            where TEvent : IDomainEvent
        {
            RequireHandler(handler);
            Type eventType = typeof(TEvent);

            if (!domainEventHandlers.TryGetValue(eventType, out List<IMessageHandlerInvoker> handlers))
            {
                handlers = new List<IMessageHandlerInvoker>();
                domainEventHandlers.Add(eventType, handlers);
            }

            handlers.Add(new DomainEventHandlerInvoker<TEvent>(handler));
        }

        internal void EnqueueIntent<TIntent>(TIntent intent) where TIntent : IIntent
        {
            intents.Enqueue(CreateEnvelope(intent));
        }

        internal void EnqueueInternalCommand<TCommand>(TCommand command) where TCommand : IInternalCommand
        {
            internalCommands.Enqueue(CreateEnvelope(command));
        }

        internal void PublishDomainEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            domainEvents.Enqueue(CreateEnvelope(domainEvent));
        }

        internal void DispatchIntents()
        {
            intents.DispatchAll((wave, envelope) =>
            {
                onDispatch?.Invoke(new MessageDispatch(MessageCategory.Intent, envelope.Message, wave));
                if (!intentHandlers.TryGetValue(envelope.MessageType, out IMessageHandlerInvoker handler))
                {
                    throw MissingHandler("intent", envelope.MessageType);
                }

                handler.Invoke(envelope.Message);
            });
        }

        internal void DrainReactions()
        {
            int reactionCycle = 0;

            try
            {
                while (HasReactions)
                {
                    if (reactionCycle >= maxReactionCycles)
                    {
                        throw new InvalidOperationException(
                            $"Maximum command/event reaction cycle count ({maxReactionCycles}) was exceeded.");
                    }

                    internalCommands.DispatchAll((wave, envelope) =>
                    {
                        onDispatch?.Invoke(new MessageDispatch(MessageCategory.InternalCommand, envelope.Message, wave));
                        if (!internalCommandHandlers.TryGetValue(
                                envelope.MessageType,
                                out IMessageHandlerInvoker handler))
                        {
                            throw MissingHandler("internal command", envelope.MessageType);
                        }

                        handler.Invoke(envelope.Message);
                    });

                    domainEvents.DispatchAll((wave, envelope) =>
                    {
                        onDispatch?.Invoke(new MessageDispatch(MessageCategory.DomainEvent, envelope.Message, wave));
                        if (!domainEventHandlers.TryGetValue(
                                envelope.MessageType,
                                out List<IMessageHandlerInvoker> handlers))
                        {
                            return;
                        }

                        for (int i = 0; i < handlers.Count; i++)
                        {
                            handlers[i].Invoke(envelope.Message);
                        }
                    });

                    reactionCycle++;
                }
            }
            catch
            {
                internalCommands.Clear();
                domainEvents.Clear();
                throw;
            }
        }

        private static THandler RequireHandler<THandler>(THandler handler) where THandler : class
        {
            return handler ?? throw new ArgumentNullException(nameof(handler));
        }

        private static MessageEnvelope CreateEnvelope<TMessage>(TMessage message)
        {
            if (ReferenceEquals(message, null))
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new MessageEnvelope(typeof(TMessage), message);
        }

        private static void AddSingleHandler(
            IDictionary<Type, IMessageHandlerInvoker> handlers,
            Type messageType,
            IMessageHandlerInvoker handler,
            string category)
        {
            if (handlers.ContainsKey(messageType))
            {
                throw new InvalidOperationException(
                    $"A handler for {category} type {messageType.FullName} is already registered.");
            }

            handlers.Add(messageType, handler);
        }

        private static InvalidOperationException MissingHandler(string category, Type messageType)
        {
            return new InvalidOperationException(
                $"No handler is registered for {category} type {messageType.FullName}.");
        }
    }
}
