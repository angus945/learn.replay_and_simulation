namespace SimulationCore.Contracts
{
    public enum CommandType
    {
        Input,
        Physics,
        Gameplay,
        LifeCycle,
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
        public readonly CommandType Type;
        public readonly int waveCount;

        CommandMetadata(ulong tick, bool isExternal, CommandType type, int waveCount)
        {
            Tick = tick;
            IsExternal = isExternal;
            Type = type;
            this.waveCount = waveCount;
        }

        public static CommandMetadata External(ulong tick, CommandType type)
        {
            return new CommandMetadata(tick, true, type, -1);
        }

        public static CommandMetadata Internal(ulong tick, CommandType type)
        {
            return new CommandMetadata(tick, false, type, -1);
        }

        public static CommandMetadata WithWave(CommandMetadata metadata, int waveCount)
        {
            return new CommandMetadata(metadata.Tick, metadata.IsExternal, metadata.Type, waveCount);
        }

        public override string ToString()
        {
            return $"Tick: {Tick}, IsExternal: {IsExternal}, Type: {Type}";
        }
    }


}
