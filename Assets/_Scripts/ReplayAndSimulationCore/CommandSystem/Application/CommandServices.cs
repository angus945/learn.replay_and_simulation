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

        public CommandServices(int maxCommandWaves = DefaultMaxCommandWaves)
        {
            if (maxCommandWaves <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCommandWaves));

            MaxCommandWaves = maxCommandWaves;
        }

        public int MaxCommandWaves { get; }

        public void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            handlerRegistry.RegisterHandler(handler);
        }

        public void EnqueueCommand(CommandMetadata data, ICommand commandInstance)
        {
            if (commandInstance == null)
                throw new ArgumentNullException(nameof(commandInstance));

            commandBuffer.Add(data, commandInstance);
        }

        public void DispatchCommands()
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

                    commandBuffer.BeginNextWave();

                    IReadOnlyList<CommandEnvelope> commands = commandBuffer.Current;
                    for (int i = 0; i < commands.Count; i++)
                    {
                        CommandEnvelope command = commands[i];
                        handlerRegistry.Dispatch(
                            command.Data,
                            command.CommandInstance);
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
