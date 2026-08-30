using System;
using System.Collections.Generic;
using DeterministicSimulation;
using WavedDispatcher;

namespace DeterministicSimulation.Framework
{
    public sealed class SimulationPipeline : ISimulationPipeline
    {
        private readonly MessagePipeline messages;
        private readonly List<IIntentSource> intentSources = new List<IIntentSource>();
        private readonly List<IPrePhysicsParticipant> prePhysicsParticipants = new List<IPrePhysicsParticipant>();
        private readonly List<IPhysicsParticipant> physicsParticipants = new List<IPhysicsParticipant>();
        private readonly List<IPostPhysicsParticipant> postPhysicsParticipants = new List<IPostPhysicsParticipant>();
        private readonly List<IStructuralCommitParticipant> structuralCommitParticipants = new List<IStructuralCommitParticipant>();
        private readonly List<IPresentationParticipant> presentationParticipants = new List<IPresentationParticipant>();

        public SimulationPipeline(int maxMessageWaves = 32, int maxReactionCycles = 32)
        {
            messages = new MessagePipeline(maxMessageWaves, maxReactionCycles);
        }

        public bool IsSealed { get; private set; }

        public void RegisterIntentHandler<TIntent>(IIntentHandler<TIntent> handler) where TIntent : IIntent
        {
            EnsureConfigurable();
            messages.RegisterIntentHandler(handler);
        }

        public void RegisterInternalCommandHandler<TCommand>(IInternalCommandHandler<TCommand> handler) where TCommand : IInternalCommand
        {
            EnsureConfigurable();
            messages.RegisterInternalCommandHandler(handler);
        }

        public void RegisterDomainEventHandler<TEvent>(IDomainEventHandler<TEvent> handler) where TEvent : IDomainEvent
        {
            EnsureConfigurable();
            messages.RegisterDomainEventHandler(handler);
        }

        public void RegisterIntentSource(IIntentSource source) => AddParticipant(intentSources, source);

        public void RegisterPrePhysicsParticipant(IPrePhysicsParticipant participant) => AddParticipant(prePhysicsParticipants, participant);

        public void RegisterPhysicsParticipant(IPhysicsParticipant participant) => AddParticipant(physicsParticipants, participant);

        public void RegisterPostPhysicsParticipant(IPostPhysicsParticipant participant) => AddParticipant(postPhysicsParticipants, participant);

        public void RegisterStructuralCommitParticipant(IStructuralCommitParticipant participant) => AddParticipant(structuralCommitParticipants, participant);

        public void RegisterPresentationParticipant(IPresentationParticipant participant) => AddParticipant(presentationParticipants, participant);

        public void Seal()
        {
            IsSealed = true;
        }

        public void EnqueueIntent<TIntent>(TIntent intent) where TIntent : IIntent
        {
            EnsureSealed();
            messages.EnqueueIntent(intent);
        }

        public void EnqueueInternalCommand<TCommand>(TCommand command) where TCommand : IInternalCommand
        {
            EnsureSealed();
            messages.EnqueueInternalCommand(command);
        }

        public void PublishDomainEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            EnsureSealed();
            messages.PublishDomainEvent(domainEvent);
        }

        internal void ExecuteTick(SimulationTick tick)
        {
            EnsureSealed();

            SimulationContext context = new SimulationContext(tick, SimulationPhase.IntentAcquisition);
            for (int i = 0; i < intentSources.Count; i++)
            {
                intentSources[i].AcquireIntents(context, this);
            }

            messages.DispatchIntents();
            messages.DrainReactions();

            ExecutePhase(prePhysicsParticipants, tick, SimulationPhase.PrePhysics,
                (participant, phaseContext) => participant.Tick(phaseContext));

            ExecutePhase(physicsParticipants, tick, SimulationPhase.Physics,
                (participant, phaseContext) => participant.Simulate(phaseContext));

            ExecutePhase(postPhysicsParticipants, tick, SimulationPhase.PostPhysics,
                (participant, phaseContext) => participant.Tick(phaseContext));

            ExecutePhase(structuralCommitParticipants, tick, SimulationPhase.StructuralCommit,
                (participant, phaseContext) => participant.Commit(phaseContext));

            context = new SimulationContext(tick, SimulationPhase.PresentationCapture);
            for (int i = 0; i < presentationParticipants.Count; i++)
            {
                presentationParticipants[i].CaptureTickState(context);
            }
        }

        internal void Render(SimulationTick tick, float interpolationAlpha)
        {
            EnsureSealed();
            SimulationContext context = new SimulationContext(tick, SimulationPhase.PresentationRender);

            for (int i = 0; i < presentationParticipants.Count; i++)
            {
                presentationParticipants[i].Render(context, interpolationAlpha);
            }
        }

        private void ExecutePhase<TParticipant>(IReadOnlyList<TParticipant> participants, SimulationTick tick, SimulationPhase phase, Action<TParticipant, SimulationContext> execute)
        {
            SimulationContext context = new SimulationContext(tick, phase);
            for (int i = 0; i < participants.Count; i++)
            {
                execute(participants[i], context);
            }

            messages.DrainReactions();
        }

        private void AddParticipant<TParticipant>(ICollection<TParticipant> participants, TParticipant participant) where TParticipant : class
        {
            EnsureConfigurable();

            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (participants.Contains(participant))
            {
                throw new InvalidOperationException(
                    $"Participant {participant.GetType().FullName} is already registered for this phase.");
            }

            participants.Add(participant);
        }

        private void EnsureConfigurable()
        {
            if (IsSealed)
            {
                throw new InvalidOperationException("The simulation pipeline is sealed.");
            }
        }

        private void EnsureSealed()
        {
            if (!IsSealed)
            {
                throw new InvalidOperationException(
                    "Seal the simulation pipeline before enqueueing messages or advancing the simulation.");
            }
        }
    }
}
