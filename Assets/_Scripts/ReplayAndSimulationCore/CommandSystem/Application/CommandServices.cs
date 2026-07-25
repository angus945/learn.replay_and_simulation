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

        public CommandServices(int maxCommandWaves = DefaultMaxCommandWaves)
        {
            if (maxCommandWaves <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCommandWaves));

            MaxCommandWaves = maxCommandWaves;
        }

        public int MaxCommandWaves { get; }

        public void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            handlerRegistry.RegisterCommandHandler(handler);
        }
        public void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            handlerRegistry.RegisterEventHandler(handler);
        }

        public void EnqueueCommand<T>(CommandMetadata data, T commandInstance) where T : ICommand
        {
            if (commandInstance == null)
                throw new ArgumentNullException(nameof(commandInstance));

            if (typeof(T) == typeof(IEvent))
            {
                throw new InvalidOperationException($"Cannot enqueue an event as a command. Use EnqueueEvent instead.");
            }

            commandBuffer.Add(CommandMetadata.WithType(data, CommandType.Command), commandInstance);
        }
        public void EnqueueEvent<T>(CommandMetadata data, T eventInstance) where T : IEvent
        {
            if (eventInstance == null)
                throw new ArgumentNullException(nameof(eventInstance));

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
