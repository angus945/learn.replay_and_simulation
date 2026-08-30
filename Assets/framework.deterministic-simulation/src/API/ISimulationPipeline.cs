using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    /// <summary>
    /// Public composition and message-submission API for a deterministic simulation.
    /// Registration is valid only before Seal; message submission is valid only after Seal.
    /// </summary>
    public interface ISimulationPipeline : IIntentSink, IInternalCommandSink, IDomainEventSink
    {
        bool IsSealed { get; }

        void RegisterIntentHandler<TIntent>(IIntentHandler<TIntent> handler)
            where TIntent : IIntent;

        void RegisterInternalCommandHandler<TCommand>(IInternalCommandHandler<TCommand> handler)
            where TCommand : IInternalCommand;

        void RegisterDomainEventHandler<TEvent>(IDomainEventHandler<TEvent> handler)
            where TEvent : IDomainEvent;

        void RegisterIntentSource(IIntentSource source);
        void RegisterPrePhysicsParticipant(IPrePhysicsParticipant participant);
        void RegisterPhysicsParticipant(IPhysicsParticipant participant);
        void RegisterPostPhysicsParticipant(IPostPhysicsParticipant participant);
        void RegisterStructuralCommitParticipant(IStructuralCommitParticipant participant);
        void RegisterPresentationParticipant(IPresentationParticipant participant);

        void Seal();
    }
}
