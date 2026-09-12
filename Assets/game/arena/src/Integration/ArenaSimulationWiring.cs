using System.Globalization;
using Arena.Application;
using Arena.Domain;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using Module.Verification.RuntimeControl;

namespace Arena.Integration
{
    public readonly struct ArenaFactMessage : IDomainEvent
    {
        public ArenaFactMessage(ArenaFact fact, long sequence, ulong tick) { Fact = fact; Sequence = sequence; Tick = tick; }
        public ArenaFact Fact { get; }
        public long Sequence { get; }
        public ulong Tick { get; }
    }
    public readonly struct RespawnCommand : IInternalCommand
    {
        public RespawnCommand(ulong tick, long sequence) { Tick = tick; Sequence = sequence; }
        public ulong Tick { get; }
        public long Sequence { get; }
    }
    public readonly struct ArenaLifecycleMessage : IDomainEvent
    {
        public ArenaLifecycleMessage(string code, ulong actor = 0, long sequence = 0) { Code = code; Actor = actor; Sequence = sequence; }
        public string Code { get; }
        public ulong Actor { get; }
        public long Sequence { get; }
    }
    public interface IArenaInputExecutionObserver
    {
        void OnInputExecutionStarted(ArenaInputIntent intent);
        void OnInputExecutionCompleted(ArenaInputIntent intent, ArenaOperationResult outcome);
    }
    public sealed class ArenaInputIntent : IIntent
    {
        public ArenaInputIntent(ArenaInput input, ArenaInputExecutionContext context, ArenaMessageDescription metadata, IArenaInputExecutionObserver observer)
        {
            Input = input ?? throw new System.ArgumentNullException(nameof(input));
            Context = context ?? throw new System.ArgumentNullException(nameof(context));
            Metadata = metadata ?? throw new System.ArgumentNullException(nameof(metadata));
            Observer = observer ?? throw new System.ArgumentNullException(nameof(observer));
        }

        public ArenaInput Input { get; }
        public ArenaInputExecutionContext Context { get; }
        public ArenaMessageDescription Metadata { get; }
        public IArenaInputExecutionObserver Observer { get; }
    }
    public readonly struct ArenaInputCommand : IInternalCommand
    {
        public ArenaInputCommand(ArenaInputIntent intent)
        {
            Intent = intent;
        }

        public ArenaInputIntent Intent { get; }
    }
    /// <summary>All framework interfaces live outside the application and domain assemblies.</summary>
    public static class ArenaSimulationWiring
    {
        public static void Configure(SimulationBuilder builder, ArenaRuntime runtime)
        {
            InputAdapter input = new InputAdapter(runtime, builder.Commands, builder.Events);
            DefeatReaction reaction = new DefeatReaction(runtime, builder.Commands, builder.Events);
            builder.RequireIntent<ArenaInputIntent>();
            builder.RequireCommand<ArenaInputCommand>();
            builder.RegisterIntentHandler<ArenaInputIntent>(input);
            builder.RegisterInternalCommandHandler<ArenaInputCommand>(input);
            builder.RequireCommand<RespawnCommand>();
            builder.RegisterDomainEventHandler<ArenaFactMessage>(reaction);
            builder.RegisterInternalCommandHandler<RespawnCommand>(reaction);
            builder.RegisterPrePhysicsParticipant(new MovementStep(runtime.Application));
            builder.RegisterStructuralCommitParticipant(new LifetimeCommit(runtime, builder.Events));
        }
        public static ArenaOperationResult Execute(ArenaRuntime runtime, ArenaInput input, ArenaInputExecutionContext context)
        {
            if (input == null) return new ArenaOperationResult(context.Handle.Sequence, context.TargetTick, OperationState.Rejected, "null-input", null);
            ActorId actor = input.Actor == 0 ? default : new ActorId(input.Actor);
            ActorId target = input.Target == 0 ? default : new ActorId(input.Target);
            ArenaResult result = runtime.Application.Execute(new ArenaRequest(input.Kind, actor, target, input.X, input.Y));
            foreach (ArenaFact fact in result.Facts)
                context.Events.PublishDomainEvent(new ArenaFactMessage(fact, context.Handle.Sequence, context.TargetTick));
            OperationState state = result.Decision == ArenaDecision.Accepted ? OperationState.Succeeded : OperationState.Rejected;
            return new ArenaOperationResult(context.Handle.Sequence, context.TargetTick, state, result.Code, null);
        }
        public static ArenaMessageDescription Describe(object message)
        {
            if (message is ArenaInputIntent inputIntent)
            {
                ArenaMessageDescription metadata = inputIntent.Metadata;
                return new ArenaMessageDescription(metadata.Type, inputIntent.Context.Handle.Sequence, metadata.Actor, metadata.Target, metadata.Detail);
            }
            if (message is ArenaInputCommand inputCommand)
            {
                ArenaInputIntent commandIntent = inputCommand.Intent;
                ArenaMessageDescription metadata = commandIntent.Metadata;
                return new ArenaMessageDescription(metadata.Type, commandIntent.Context.Handle.Sequence, metadata.Actor, metadata.Target, metadata.Detail);
            }
            if (message is ArenaFactMessage fact)
                return new ArenaMessageDescription(fact.Fact.Kind.ToString(), fact.Sequence, fact.Fact.Actor.Value,
                    fact.Fact.Target.Value, fact.Fact.Amount.ToString(CultureInfo.InvariantCulture));
            if (message is RespawnCommand command) return new ArenaMessageDescription("ScheduleRespawn", command.Sequence);
            if (message is ArenaLifecycleMessage lifecycle)
                return new ArenaMessageDescription("Lifecycle", lifecycle.Sequence, lifecycle.Actor, detail: lifecycle.Code);
            return null;
        }
        private sealed class InputAdapter : IIntentHandler<ArenaInputIntent>, IInternalCommandHandler<ArenaInputCommand>
        {
            private readonly ArenaRuntime runtime;
            private readonly IInternalCommandSink commands;
            private readonly IDomainEventSink events;

            public InputAdapter(ArenaRuntime runtime, IInternalCommandSink commands, IDomainEventSink events)
            {
                this.runtime = runtime;
                this.commands = commands;
                this.events = events;
            }

            public void Handle(ArenaInputIntent intent)
            {
                commands.EnqueueInternalCommand(new ArenaInputCommand(intent));
            }

            public void Handle(ArenaInputCommand command)
            {
                ArenaInputIntent intent = command.Intent;
                intent.Observer.OnInputExecutionStarted(intent);
                ArenaInputExecutionContext context = intent.Context.WithEvents(events);
                ArenaOperationResult outcome = Execute(runtime, intent.Input, context);
                intent.Observer.OnInputExecutionCompleted(intent, outcome);
            }
        }
        private sealed class MovementStep : IPrePhysicsParticipant
        {
            private readonly ArenaApplication application;
            public MovementStep(ArenaApplication application) { this.application = application; }
            public void Tick(SimulationContext context)
            {
                application.Advance(context.Tick.Number, context.Tick.DeltaTime);
            }
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
