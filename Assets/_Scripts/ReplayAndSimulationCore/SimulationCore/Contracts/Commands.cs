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

    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }

    public readonly struct CommandMetadata
    {
        public readonly ulong Tick;
        public readonly bool IsExternal;
        public readonly CommandType Type;

        public CommandMetadata(ulong tick, bool isExternal, CommandType type)
        {
            Tick = tick;
            IsExternal = isExternal;
            Type = type;
        }

        public static CommandMetadata External(ulong tick, CommandType type)
        {
            return new CommandMetadata(tick, true, type);
        }

        public static CommandMetadata Internal(ulong tick, CommandType type)
        {
            return new CommandMetadata(tick, false, type);
        }

        public override string ToString()
        {
            return $"Tick: {Tick}, IsExternal: {IsExternal}, Type: {Type}";
        }
    }


}
