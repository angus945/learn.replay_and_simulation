namespace TickCommandSystem.Contract
{
    public readonly struct CommandData
    {
        public readonly ulong Tick;
        public readonly bool IsExternal;
        public readonly CommandType Type;

        public CommandData(
            ulong tick,
            bool isExternal,
            CommandType type)
        {
            Tick = tick;
            IsExternal = isExternal;
            Type = type;
        }

        public static CommandData External(ulong tick, CommandType type)
        {
            return new CommandData(tick, true, type);
        }

        public static CommandData Internal(ulong tick, CommandType type)
        {
            return new CommandData(tick, false, type);
        }
    }

    public enum CommandType
    {
        Physics,
        Gameplay,
        LifeCycle,
    }
}
