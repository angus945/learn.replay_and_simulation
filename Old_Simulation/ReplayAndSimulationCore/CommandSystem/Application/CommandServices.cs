using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.CommandSystem.API;
using SimulationCore.CommandSystem.Domain;



namespace SimulationCore.CommandSystem.Application
{
    public sealed class CommandServices : ISimulationCommandSystem, ICommandContext
    {
        public const int DefaultMaxCommandWaves = 30;

        private readonly CommandHandlerRegistry handlerRegistry = new();
        private readonly CommandBuffer commandBuffer = new();
        private readonly CommandBuffer eventBuffer = new();
        bool registeringLocked = false;

        public CommandServices(int maxCommandWaves = DefaultMaxCommandWaves)
        {
            if (maxCommandWaves <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCommandWaves));

            MaxCommandWaves = maxCommandWaves;
            registeringLocked = false;
        }

        public int MaxCommandWaves { get; }

        public void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            if (registeringLocked)
                throw new InvalidOperationException("Cannot register command handlers after dispatching has started.");

            if (typeof(IEvent).IsAssignableFrom(typeof(TCommand)))
                throw new InvalidOperationException("Cannot register an event handler as a command handler. Use RegisterEventHandler instead.");

            handlerRegistry.RegisterCommandHandler(handler);
        }
        public void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (registeringLocked)
                throw new InvalidOperationException("Cannot register event handlers after dispatching has started.");

            handlerRegistry.RegisterEventHandler(handler);
        }

        public void EnqueueCommand<T>(CommandMetadata data, T commandInstance) where T : ICommand
        {
            if (commandInstance == null)
                throw new ArgumentNullException(nameof(commandInstance));

            if (typeof(IEvent).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException("Cannot enqueue an event as a command. Use EnqueueEvent instead.");

            registeringLocked = true;
            commandBuffer.Add(CommandMetadata.WithType(data, CommandType.Command), commandInstance);
        }
        public void EnqueueEvent<T>(CommandMetadata data, T eventInstance) where T : IEvent
        {
            if (eventInstance == null)
                throw new ArgumentNullException(nameof(eventInstance));

            registeringLocked = true;
            eventBuffer.Add(CommandMetadata.WithType(data, CommandType.Event), eventInstance);
        }

        public void DispatchAll()
        {
            int waveCount = 0;

            try
            {
                while (commandBuffer.HasPending)
                {
                    if (waveCount >= MaxCommandWaves)
                    {
                        throw new InvalidOperationException(
                            $"Max command dispatch waves reached. Max waves: {MaxCommandWaves}.");
                    }

                    IReadOnlyList<CommandEnvelope> events = eventBuffer.Begin();
                    for (int i = 0; i < events.Count; i++)
                    {
                        CommandEnvelope @event = events[i];
                        CommandMetadata eventMeta = CommandMetadata.WithWave(@event.Data, waveCount);
                        handlerRegistry.DispatchEvent(eventMeta, (IEvent)@event.CommandInstance);
                    }

                    IReadOnlyList<CommandEnvelope> commands = commandBuffer.Begin();
                    for (int i = 0; i < commands.Count; i++)
                    {
                        CommandEnvelope command = commands[i];
                        CommandMetadata commandMeta = CommandMetadata.WithWave(command.Data, waveCount);
                        handlerRegistry.DispatchCommand(commandMeta, command.CommandInstance);
                    }

                    waveCount++;
                }
            }
            finally
            {
                commandBuffer.ClearAll();
            }
        }
    }
}
