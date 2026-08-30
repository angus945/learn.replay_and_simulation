using System;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using InvariantChecks;

namespace Testability.Templates
{
    /// <summary>Integration template, not a domain base class. All codec results must be deterministic.
    /// Scenario/input decoding must create independent values; observations must be immutable.</summary>
    public abstract class ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation>
        : SimulationDefinition<TWorld, TScenario>, ISimulationObserver<TWorld, TObservation> where TWorld : class
    {
        public abstract string PolicyId { get; }
        protected abstract string EncodeScenario(TScenario scenario);
        protected abstract TScenario DecodeScenario(string payload);
        protected abstract string EncodeInput(TInput input);
        protected abstract TInput DecodeInput(string payload);
        protected abstract TObservation CaptureObservation(TWorld world);
        protected abstract byte[] EncodeCanonicalState(TObservation observation);
        protected abstract void ConfigureInvariants(InvariantRegistry<TObservation> invariants);
        protected abstract void ConfigureWorld(SimulationBuilder builder, TWorld world, TScenario scenario);
        /// <summary>Override this overload when the integration needs envelope identity for event causation.</summary>
        protected virtual InputOutcome ExecuteInput(TWorld world, TInput input, InputExecutionContext context)
            => ExecuteInput(world, input, context.Events);
        /// <summary>Compatibility hook for definitions that do not need input envelope metadata.</summary>
        protected virtual InputOutcome ExecuteInput(TWorld world, TInput input, IDomainEventSink events)
            => throw new NotSupportedException("The definition must override an ExecuteInput overload.");
        protected virtual TemplateLimits CreateDefaultLimits(TScenario scenario) => new TemplateLimits();
        protected virtual TemplateTraceMetadata DescribeInput(TInput input) => null;
        protected virtual TemplateTraceMetadata DescribeMessage(object message) => null;

        protected sealed override void Configure(SimulationBuilder builder, TWorld world, TScenario scenario)
        {
            InputAdapter adapter = new InputAdapter(this, world, builder.Commands, builder.Events);
            builder.RequireIntent<InputIntent>(); builder.RequireCommand<InputCommand>();
            builder.RegisterIntentHandler<InputIntent>(adapter);
            builder.RegisterInternalCommandHandler<InputCommand>(adapter);
            ConfigureWorld(builder, world, scenario);
        }

        public TestableSimulationSession<TWorld, TScenario, TInput, TObservation> CreateTestSession(TScenario scenario,
            TemplateLimits limits = null)
        {
            if (ReferenceEquals(scenario, null)) throw new ArgumentNullException(nameof(scenario));
            return new TestableSimulationSession<TWorld, TScenario, TInput, TObservation>(this, scenario, limits);
        }
        public TemplateReplay<TWorld, TScenario, TInput, TObservation> CreateReplay(TemplateRecording recording)
            => new TemplateReplay<TWorld, TScenario, TInput, TObservation>(this, recording);

        public TObservation Observe(TWorld world) => CaptureObservation(world);
        internal string SaveScenario(TScenario value) => EncodeScenario(value);
        internal TScenario LoadScenario(string value) => DecodeScenario(value);
        internal string SaveInput(TInput value) => EncodeInput(value);
        internal TInput LoadInput(string value) => DecodeInput(value);
        internal TemplateLimits DefaultLimits(TScenario scenario)
            => CreateDefaultLimits(scenario) ?? throw new InvalidOperationException("Default limits cannot be null.");
        internal TemplateTraceMetadata InputMetadata(TInput input)
            => DescribeInput(input) ?? new TemplateTraceMetadata(typeof(TInput).Name);
        internal TemplateTraceMetadata DispatchMetadata(object message)
        {
            InputIntent intent = message as InputIntent;
            if (message is InputCommand command) intent = command.Intent;
            if (intent == null) return DescribeMessage(message);
            TemplateTraceMetadata metadata = intent.Metadata;
            return new TemplateTraceMetadata(metadata.Type, intent.Context.Sequence, metadata.Actor, metadata.Target, metadata.Detail);
        }
        internal string Hash(TObservation value) => StateDigest.Compute(EncodeCanonicalState(value)
            ?? throw new InvalidOperationException("Canonical state bytes cannot be null."));
        internal InvariantRegistry<TObservation> CreateChecks()
        {
            InvariantRegistry<TObservation> checks = new InvariantRegistry<TObservation>();
            ConfigureInvariants(checks); checks.Seal(); return checks;
        }
        internal float TickDelta(TScenario scenario) => GetTickDelta(scenario);

        internal sealed class InputIntent : IIntent
        {
            internal TInput Input;
            internal InputExecutionContext Context;
            internal TemplateTraceMetadata Metadata;
            internal Action Begin;
            internal Action<InputOutcome> Complete;
        }
        private readonly struct InputCommand : IInternalCommand
        {
            internal InputCommand(InputIntent intent) { Intent = intent; }
            internal InputIntent Intent { get; }
        }
        private sealed class InputAdapter : IIntentHandler<InputIntent>, IInternalCommandHandler<InputCommand>
        {
            private readonly ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition;
            private readonly TWorld world;
            private readonly IInternalCommandSink commands;
            private readonly IDomainEventSink events;
            internal InputAdapter(ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition,
                TWorld world, IInternalCommandSink commands, IDomainEventSink events)
            { this.definition = definition; this.world = world; this.commands = commands; this.events = events; }
            public void Handle(InputIntent intent) => commands.EnqueueInternalCommand(new InputCommand(intent));
            public void Handle(InputCommand command)
            {
                command.Intent.Begin();
                InputOutcome outcome = definition.ExecuteInput(world, command.Intent.Input, command.Intent.Context.WithEvents(events));
                if (outcome == null || string.IsNullOrWhiteSpace(outcome.Code) || !Enum.IsDefined(typeof(ActionStatus), outcome.Status))
                    throw new InvalidOperationException("ExecuteInput must return a valid outcome.");
                command.Intent.Complete(outcome);
            }
        }
    }
}
