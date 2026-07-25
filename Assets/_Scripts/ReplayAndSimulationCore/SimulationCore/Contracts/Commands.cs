namespace SimulationCore.Contracts
{
    public enum CommandSource
    {
        Input,
        Physics,
        Gameplay,
        LifeCycle,
    }
    public enum CommandType
    {
        None,
        Command,
        Event
    }

    public interface ICommand { }
    public interface IEvent : ICommand { }

    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }
    public interface IEventHandler<TEvent> where TEvent : IEvent
    {
        void Handle(TEvent @event);
    }

    public readonly struct CommandMetadata
    {
        public readonly ulong Tick;
        public readonly bool IsExternal;
        public readonly CommandSource Source;
        public readonly CommandType Type;
        public readonly int waveCount;

        CommandMetadata(ulong tick, bool isExternal, CommandSource source, CommandType type, int waveCount)
        {
            Tick = tick;
            IsExternal = isExternal;
            Source = source;
            Type = type;
            this.waveCount = waveCount;
        }

        public static CommandMetadata External(ulong tick, CommandSource source)
        {
            return new CommandMetadata(tick, true, source, CommandType.None, -1);
        }

        public static CommandMetadata Internal(ulong tick, CommandSource type)
        {
            return new CommandMetadata(tick, false, type, CommandType.None, -1);
        }

        public static CommandMetadata WithType(CommandMetadata metadata, CommandType type)
        {
            return new CommandMetadata(metadata.Tick, metadata.IsExternal, metadata.Source, type, metadata.waveCount);
        }

        public static CommandMetadata WithWave(CommandMetadata metadata, int waveCount)
        {
            return new CommandMetadata(metadata.Tick, metadata.IsExternal, metadata.Source, metadata.Type, waveCount);
        }

        public override string ToString()
        {
            return $"Tick: {Tick}, IsExternal: {IsExternal}, Type: {Source}";
        }
    }


}
