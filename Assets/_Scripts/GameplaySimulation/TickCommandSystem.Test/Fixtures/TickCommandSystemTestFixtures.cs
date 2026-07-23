using System;
using System.Collections.Generic;
using TickCommandSystem.Contract;

namespace TickCommandSystem.Test.Fixtures
{
    internal readonly struct TestCommand : ICommand
    {
        public readonly int Value;

        public TestCommand(int value)
        {
            Value = value;
        }
    }

    internal readonly struct SecondaryTestCommand : ICommand
    {
        public readonly int Value;

        public SecondaryTestCommand(int value)
        {
            Value = value;
        }
    }

    internal readonly struct CommandRecord
    {
        public readonly string Label;
        public readonly CommandData Data;
        public readonly ICommand CommandInstance;

        public CommandRecord(
            string label,
            CommandData data,
            ICommand commandInstance)
        {
            Label = label;
            Data = data;
            CommandInstance = commandInstance;
        }
    }

    internal sealed class RecordingCommandHandler<TCommand> :
        ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        private readonly List<CommandRecord> records;
        private readonly string label;
        private readonly Action<CommandData, TCommand> onHandle;

        public RecordingCommandHandler(
            List<CommandRecord> records,
            string label,
            Action<CommandData, TCommand> onHandle = null)
        {
            this.records = records ??
                throw new ArgumentNullException(nameof(records));
            this.label = label ??
                throw new ArgumentNullException(nameof(label));
            this.onHandle = onHandle;
        }

        public void Handle(CommandData data, TCommand command)
        {
            records.Add(new CommandRecord(label, data, command));
            onHandle?.Invoke(data, command);
        }
    }

    internal sealed class ThrowingCommandHandler<TCommand> :
        ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public void Handle(CommandData data, TCommand command)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }
}
