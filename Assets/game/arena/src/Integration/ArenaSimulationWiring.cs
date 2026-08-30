using System.Globalization;
using Arena.Application;
using Arena.Domain;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using Testability;
using Testability.Templates;

namespace Arena.Integration
{
    public readonly struct ArenaFactMessage : IDomainEvent
    {
        public ArenaFactMessage(ArenaFact fact, ulong sequence, ulong tick) { Fact = fact; Sequence = sequence; Tick = tick; }
        public ArenaFact Fact { get; }
        public ulong Sequence { get; }
        public ulong Tick { get; }
    }
    public readonly struct RespawnCommand : IInternalCommand
    {
        public RespawnCommand(ulong tick, ulong sequence) { Tick = tick; Sequence = sequence; }
        public ulong Tick { get; }
        public ulong Sequence { get; }
    }
    public readonly struct ArenaLifecycleMessage : IDomainEvent
    {
        public ArenaLifecycleMessage(string code, ulong actor = 0, ulong sequence = 0) { Code = code; Actor = actor; Sequence = sequence; }
        public string Code { get; }
        public ulong Actor { get; }
        public ulong Sequence { get; }
    }
    /// <summary>All framework interfaces live outside the application and domain assemblies.</summary>
    public static class ArenaSimulationWiring
    {
        public static void Configure(SimulationBuilder builder, ArenaRuntime runtime)
        {
            DefeatReaction reaction = new DefeatReaction(runtime, builder.Commands, builder.Events);
            builder.RequireCommand<RespawnCommand>();
            builder.RegisterDomainEventHandler<ArenaFactMessage>(reaction);
            builder.RegisterInternalCommandHandler<RespawnCommand>(reaction);
            builder.RegisterPrePhysicsParticipant(new MovementStep(runtime.Application));
            builder.RegisterStructuralCommitParticipant(new LifetimeCommit(runtime, builder.Events));
        }
        public static InputOutcome Execute(ArenaRuntime runtime, ArenaInput input, InputExecutionContext context)
        {
            if (input == null) return new InputOutcome(ActionStatus.InvalidRequest, "null-input");
            ActorId actor = input.Actor == 0 ? default : new ActorId(input.Actor);
            ActorId target = input.Target == 0 ? default : new ActorId(input.Target);
            ArenaResult result = runtime.Application.Execute(new ArenaRequest(input.Kind, actor, target, input.X, input.Y));
            foreach (ArenaFact fact in result.Facts)
                context.Events.PublishDomainEvent(new ArenaFactMessage(fact, context.Sequence, context.TargetTick));
            ActionStatus status = result.Decision == ArenaDecision.Accepted ? ActionStatus.Accepted
                : result.Decision == ArenaDecision.Rejected ? ActionStatus.Rejected : ActionStatus.InvalidRequest;
            return new InputOutcome(status, result.Code);
        }
        public static TemplateTraceMetadata Describe(object message)
        {
            if (message is ArenaFactMessage fact)
                return new TemplateTraceMetadata(fact.Fact.Kind.ToString(), fact.Sequence, fact.Fact.Actor.Value,
                    fact.Fact.Target.Value, fact.Fact.Amount.ToString(CultureInfo.InvariantCulture));
            if (message is RespawnCommand command) return new TemplateTraceMetadata("ScheduleRespawn", command.Sequence);
            if (message is ArenaLifecycleMessage lifecycle)
                return new TemplateTraceMetadata("Lifecycle", lifecycle.Sequence, lifecycle.Actor, detail: lifecycle.Code);
            return null;
        }
        private sealed class MovementStep : IPrePhysicsParticipant
        {
            private readonly ArenaApplication application;
            public MovementStep(ArenaApplication application) { this.application = application; }
            public void Tick(SimulationContext context) => application.Advance(context.Tick.Number, context.Tick.DeltaTime);
        }
        private sealed class DefeatReaction : IDomainEventHandler<ArenaFactMessage>, IInternalCommandHandler<RespawnCommand>
        {
            private readonly ArenaRuntime runtime;
            private readonly IInternalCommandSink commands;
            private readonly IDomainEventSink events;
            public DefeatReaction(ArenaRuntime runtime, IInternalCommandSink commands, IDomainEventSink events)
            { this.runtime = runtime; this.commands = commands; this.events = events; }
            public void Handle(ArenaFactMessage message)
            {
                if (message.Fact.Kind != ArenaFactKind.Defeated) return;
                bool respawn = runtime.Application.OnDefeated(message.Fact.Target);
                if (respawn) commands.EnqueueInternalCommand(new RespawnCommand(message.Tick, message.Sequence));
            }
            public void Handle(RespawnCommand command)
            {
                bool scheduled = runtime.Application.ScheduleRespawn(command.Tick);
                events.PublishDomainEvent(new ArenaLifecycleMessage(scheduled ? "respawn.scheduled" : "spawn.budget", sequence: command.Sequence));
            }
        }
        private sealed class LifetimeCommit : IStructuralCommitParticipant
        {
            private readonly ArenaRuntime runtime;
            private readonly IDomainEventSink events;
            public LifetimeCommit(ArenaRuntime runtime, IDomainEventSink events) { this.runtime = runtime; this.events = events; }
            public void Commit(SimulationContext context)
            {
                ulong lastId = runtime.Application.LastActorId;
                System.Collections.Generic.List<Actor> before = new System.Collections.Generic.List<Actor>(runtime.Application.Actors);
                runtime.Application.Commit(context.Tick.Number);
                foreach (Actor actor in before)
                    if (!runtime.Lifecycle.IsActive(actor.Id)) events.PublishDomainEvent(new ArenaLifecycleMessage("destroy.committed", actor.Id.Value));
                foreach (Actor actor in runtime.Application.Actors)
                    if (actor.Id.Value > lastId) events.PublishDomainEvent(new ArenaLifecycleMessage("spawn.committed", actor.Id.Value));
            }
        }
    }
}
