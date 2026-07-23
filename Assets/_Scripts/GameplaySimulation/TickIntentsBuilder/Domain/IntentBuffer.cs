using System;
using System.Collections.Generic;
using TickCommandSystem.API;
using TickCommandSystem.Contract;

namespace TickIntentsBuilder.Domain
{
    internal sealed class ProducedCommandBuffer
    {
        readonly List<ICommand> pendingCommands = new();
        readonly List<ICommand> committedCommands = new();

        internal ulong LastCommittedTick { get; private set; }
        internal bool HasCommittedTick { get; private set; }

        internal void BeginProduce()
        {
            pendingCommands.Clear();
        }

        internal void Submit(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            pendingCommands.Add(command);
        }

        internal void Commit(ulong tick)
        {
            if (HasCommittedTick && tick <= LastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"Command tick must increase monotonically. " +
                    $"Last tick: {LastCommittedTick}, requested tick: {tick}."
                );
            }

            committedCommands.Clear();
            committedCommands.AddRange(pendingCommands);

            LastCommittedTick = tick;
            HasCommittedTick = true;
        }

        internal void EnqueueCommitted(
            ITickCommandQueue commandQueue,
            CommandType type)
        {
            if (commandQueue == null)
                throw new ArgumentNullException(nameof(commandQueue));

            if (!HasCommittedTick)
            {
                throw new InvalidOperationException(
                    "Cannot enqueue commands because no tick has been committed yet."
                );
            }

            CommandData data = CommandData.External(LastCommittedTick, type);
            for (int i = 0; i < committedCommands.Count; i++)
            {
                commandQueue.EnqueueCommand(data, committedCommands[i]);
            }
        }
    }
}
