namespace DeterministicSimulation
{
    /// <summary>API used by external input adapters and replay sources to submit intents.</summary>
    public interface IIntentSink
    {
        void EnqueueIntent<TIntent>(TIntent intent) where TIntent : IIntent;
    }

    /// <summary>API used by simulation code to request an internal state transition.</summary>
    public interface IInternalCommandSink
    {
        void EnqueueInternalCommand<TCommand>(TCommand command) where TCommand : IInternalCommand;
    }

    /// <summary>API used by the domain to publish an immutable fact that has occurred.</summary>
    public interface IDomainEventSink
    {
        void PublishDomainEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }
}
