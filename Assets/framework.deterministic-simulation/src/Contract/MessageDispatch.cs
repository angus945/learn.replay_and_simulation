namespace DeterministicSimulation.Framework
{
    public enum MessageCategory { Intent, InternalCommand, DomainEvent }

    /// <summary>Synchronous diagnostic notification before dispatch; observer must not mutate simulation.</summary>
    public readonly struct MessageDispatch
    {
        public MessageDispatch(MessageCategory category, object message, int wave)
        { Category = category; Message = message; Wave = wave; }
        public MessageCategory Category { get; }
        public object Message { get; }
        public int Wave { get; }
    }
}
