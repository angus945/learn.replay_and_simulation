using System;
using System.Collections.Generic;
using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    /// <summary>Configuration-only surface, sealed by the session after Configure returns.</summary>
    public sealed class SimulationBuilder
    {
        private readonly SimulationPipeline pipeline;
        private readonly HashSet<Type> intents = new HashSet<Type>();
        private readonly HashSet<Type> commands = new HashSet<Type>();
        private readonly List<Action<List<string>>> requirements = new List<Action<List<string>>>();
        private bool sealedBuilder;

        internal SimulationBuilder(Action<SimulationPhase, bool> onPhase = null, Action<MessageDispatch> onDispatch = null)
        { pipeline = new SimulationPipeline(onPhase: onPhase, onDispatch: onDispatch); }

        public IInternalCommandSink Commands => pipeline;
        public IDomainEventSink Events => pipeline;

        public void RequireIntent<T>() where T : IIntent
        {
            EnsureOpen();
            requirements.Add(errors => { if (!intents.Contains(typeof(T))) errors.Add("Missing intent handler: " + typeof(T).FullName); });
        }
        public void RequireCommand<T>() where T : IInternalCommand
        {
            EnsureOpen();
            requirements.Add(errors => { if (!commands.Contains(typeof(T))) errors.Add("Missing command handler: " + typeof(T).FullName); });
        }
        public void RegisterIntentHandler<T>(IIntentHandler<T> handler) where T : IIntent
        { EnsureOpen(); pipeline.RegisterIntentHandler(handler); intents.Add(typeof(T)); }
        public void RegisterInternalCommandHandler<T>(IInternalCommandHandler<T> handler) where T : IInternalCommand
        { EnsureOpen(); pipeline.RegisterInternalCommandHandler(handler); commands.Add(typeof(T)); }
        public void RegisterDomainEventHandler<T>(IDomainEventHandler<T> handler) where T : IDomainEvent
        { EnsureOpen(); pipeline.RegisterDomainEventHandler(handler); }
        public void RegisterIntentSource(IIntentSource source)
        { EnsureOpen(); pipeline.RegisterIntentSource(source); }
        public void RegisterPrePhysicsParticipant(IPrePhysicsParticipant participant)
        { EnsureOpen(); pipeline.RegisterPrePhysicsParticipant(participant); }
        public void RegisterPhysicsParticipant(IPhysicsParticipant participant)
        { EnsureOpen(); pipeline.RegisterPhysicsParticipant(participant); }
        public void RegisterPostPhysicsParticipant(IPostPhysicsParticipant participant)
        { EnsureOpen(); pipeline.RegisterPostPhysicsParticipant(participant); }
        public void RegisterStructuralCommitParticipant(IStructuralCommitParticipant participant)
        { EnsureOpen(); pipeline.RegisterStructuralCommitParticipant(participant); }
        public void RegisterPresentationParticipant(IPresentationParticipant participant)
        { EnsureOpen(); pipeline.RegisterPresentationParticipant(participant); }

        internal SimulationPipeline Build()
        {
            EnsureOpen();
            sealedBuilder = true;
            List<string> errors = new List<string>();
            foreach (Action<List<string>> requirement in requirements) requirement(errors);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            pipeline.Seal();
            return pipeline;
        }
        private void EnsureOpen()
        {
            if (sealedBuilder) throw new InvalidOperationException("Simulation configuration is sealed.");
        }
    }
}
