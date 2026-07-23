using System.Collections.Generic;
using TickCommandSystem.Contract;

namespace TickCommandSystem.Domain
{
    internal sealed class CommandEnvelope
    {
        internal CommandData Data;
        internal ICommand CommandInstance;

        internal void Set(CommandData data, ICommand commandInstance)
        {
            Data = data;
            CommandInstance = commandInstance;
        }

        internal void Clear()
        {
            Data = default;
            CommandInstance = null;
        }
    }

    internal sealed class CommandPool
    {
        private readonly Stack<CommandEnvelope> pool = new();

        internal CommandEnvelope Get(
            CommandData data,
            ICommand commandInstance)
        {
            CommandEnvelope command = pool.Count > 0
                ? pool.Pop()
                : new CommandEnvelope();

            command.Set(data, commandInstance);
            return command;
        }

        internal void Release(CommandEnvelope command)
        {
            command.Clear();
            pool.Push(command);
        }
    }

    internal sealed class CommandBuffer
    {
        private readonly CommandPool commandPool = new();
        private List<CommandEnvelope> current = new();
        private List<CommandEnvelope> pending = new();

        internal IReadOnlyList<CommandEnvelope> Current => current;
        internal bool HasPending => pending.Count > 0;

        internal void Add(CommandData data, ICommand commandInstance)
        {
            pending.Add(commandPool.Get(data, commandInstance));
        }

        internal void BeginNextWave()
        {
            ReleaseCurrent();
            (current, pending) = (pending, current);
        }

        internal void ClearAll()
        {
            ReleaseCurrent();

            for (int i = 0; i < pending.Count; i++)
            {
                commandPool.Release(pending[i]);
            }

            pending.Clear();
        }

        private void ReleaseCurrent()
        {
            for (int i = 0; i < current.Count; i++)
            {
                commandPool.Release(current[i]);
            }

            current.Clear();
        }
    }
}
