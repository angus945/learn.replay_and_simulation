namespace DeterministicSimulation
{
    /// <summary>An external decision requested by a player, AI, network peer, or replay source.</summary>
    public interface IIntent { }

    /// <summary>An internal request for one authoritative state transition.</summary>
    public interface IInternalCommand { }

    /// <summary>An immutable fact that has already occurred in the simulation domain.</summary>
    public interface IDomainEvent { }

    public interface IIntentHandler<in TIntent> where TIntent : IIntent
    {
        void Handle(TIntent intent);
    }

    public interface IInternalCommandHandler<in TCommand> where TCommand : IInternalCommand
    {
        void Handle(TCommand command);
    }

    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        void Handle(TEvent domainEvent);
    }

}
