using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeterministicSimulation.Framework.Tests
{
    /// <summary>Pure C# behavior checks for the public low-level driver and phase contracts.</summary>
    public static class CoreHardeningContractChecks
    {
        public static void LowLevelClockAndFailure()
        {
            SimulationPipeline empty = new SimulationPipeline();
            empty.Seal();
            SimulationRunner clock = new SimulationRunner(empty, .25f, maxTicksPerAdvanceTime: 2);
            clock.AdvanceTime(1.125f);
            Check(clock.TickNumber == 2 && clock.Accumulator == .625f, "Clock must bound work and retain catch-up debt.");
            clock.AdvanceTime(0);
            Check(clock.TickNumber == 4 && clock.Accumulator == .125f, "Clock lost retained debt.");
            Expect<ArgumentOutOfRangeException>(() => clock.AdvanceTime(float.PositiveInfinity));
            Check(clock.Failure == null, "Invalid elapsed time must not fault a healthy runner.");

            SimulationPipeline pipeline = new SimulationPipeline();
            List<string> observed = new List<string>();
            ReactionHandler reactions = new ReactionHandler(pipeline, observed);
            ApplicationException original = new ApplicationException("input failed after producing work");
            pipeline.RegisterIntentHandler(new CallbackHandler(() =>
            {
                pipeline.EnqueueInternalCommand(new Reaction("abandoned"));
                throw original;
            }));
            pipeline.RegisterInternalCommandHandler<Reaction>(reactions);
            pipeline.RegisterDomainEventHandler<Signal>(reactions);
            pipeline.Seal();
            SimulationRunner runner = new SimulationRunner(pipeline, .25f);
            runner.AdvanceTick();
            pipeline.EnqueueIntent(new Input());
            Expect<ApplicationException>(() => runner.AdvanceTime(.5f));
            Check(ReferenceEquals(runner.Failure, original), "Runner did not retain the first failure.");
            Check(runner.TickNumber == 2 && runner.LastCompletedTick == 1, "Attempted and completed ticks must remain distinct.");
            Check(runner.Accumulator == 0, "Failed tick debt must not be retried.");
            Expect<InvalidOperationException>(() => runner.AdvanceTick());
            Expect<InvalidOperationException>(() => runner.AdvanceTime(0));
            Expect<InvalidOperationException>(() => runner.UpdatePresentation());
            Check(observed.Count == 0 && runner.TickNumber == 2, "Failed runner executed abandoned commands.");
        }

        public static void LowLevelReentryAndRenderFailure()
        {
            SimulationPipeline pipeline = new SimulationPipeline();
            SimulationRunner runner = null;
            int calls = 0;
            pipeline.RegisterIntentHandler(new CallbackHandler(() =>
            {
                calls++;
                Expect<InvalidOperationException>(() => runner.AdvanceTick());
                Expect<InvalidOperationException>(() => runner.AdvanceTime(0));
                Expect<InvalidOperationException>(() => runner.UpdatePresentation());
            }));
            pipeline.Seal();
            runner = new SimulationRunner(pipeline);
            pipeline.EnqueueIntent(new Input());
            pipeline.EnqueueIntent(new Input());
            runner.AdvanceTick();
            Check(calls == 2 && runner.LastCompletedTick == 1 && runner.Failure == null, "Rejected reentry corrupted the outer tick.");
            Task.Run(() => Expect<InvalidOperationException>(() => runner.AdvanceTick())).GetAwaiter().GetResult();
            Check(runner.TickNumber == 1, "Foreign thread advanced the low-level runner.");

            SimulationPipeline viewPipeline = new SimulationPipeline();
            viewPipeline.RegisterPresentationParticipant(new FailingPresentation());
            viewPipeline.Seal();
            SimulationRunner viewRunner = new SimulationRunner(viewPipeline);
            Expect<ApplicationException>(() => viewRunner.UpdatePresentation());
            Check(viewRunner.Failure is ApplicationException && viewRunner.TickNumber == 0, "Render failure changed authoritative time.");
            Expect<InvalidOperationException>(() => viewRunner.AdvanceTick());
        }

        public static void SessionOwnerThread()
        {
            CounterDefinition definition = new CounterDefinition();
            using (SimulationSession<Counter, float> session = definition.CreateSession(.25f))
            {
                CounterObserver observer = new CounterObserver();
                Action[] operations =
                {
                    () => session.EnqueueIntent(new Input()),
                    () => session.Step(),
                    () => session.Observe(observer),
                    () => session.Render(0),
                    () => session.Stop(),
                    () => session.Reset(.5f),
                    () => session.CreateRealtimeRunner(),
                    () => session.Dispose()
                };
                Task.Run(() =>
                {
                    foreach (Action operation in operations) Expect<InvalidOperationException>(operation);
                }).GetAwaiter().GetResult();
                Check(session.State == SimulationSessionState.Running && session.TickNumber == 0, "Foreign thread changed session state.");
                session.EnqueueIntent(new Input());
                session.Step();
                Check(session.Observe(observer) == 1, "Thread rejection damaged the owner-thread session.");
                session.Reset(.5f);
                Check(session.Observe(observer) == 0, "Reset must remain available on the owner thread.");
            }
        }

        public static void ParticipantOrderAndReactionTiming()
        {
            List<string> observed = new List<string>();
            SimulationPipeline pipeline = new SimulationPipeline();
            ReactionHandler reactions = new ReactionHandler(pipeline, observed);
            pipeline.RegisterInternalCommandHandler<Reaction>(reactions);
            pipeline.RegisterDomainEventHandler<Signal>(reactions);
            PhaseParticipant first = new PhaseParticipant("A", pipeline, observed);
            PhaseParticipant second = new PhaseParticipant("B", pipeline, observed);
            RegisterPhases(pipeline, first);
            RegisterPhases(pipeline, second);
            pipeline.Seal();
            pipeline.PublishDomainEvent(new Signal("start"));
            SimulationRunner runner = new SimulationRunner(pipeline);
            runner.AdvanceTick();
            runner.UpdatePresentation();
            string expected =
                "event:start,command:followup,event:followup," +
                "pre:A,pre:B,command:pre:A,command:pre:B,event:pre:A,event:pre:B," +
                "physics:A,physics:B,command:physics:A,command:physics:B,event:physics:A,event:physics:B," +
                "post:A,post:B,command:post:A,command:post:B,event:post:A,event:post:B," +
                "commit:A,commit:B,command:commit:A,command:commit:B,event:commit:A,event:commit:B," +
                "capture:A,capture:B,render:A,render:B";
            Check(string.Join(",", observed) == expected,
                "Each phase must run every participant in registration order before draining reactions; event-only work must also drain.");

            List<string> limitedTrace = new List<string>();
            SimulationPipeline limited = new SimulationPipeline(maxReactionCycles: 1);
            ReactionHandler limitedReactions = new ReactionHandler(limited, limitedTrace);
            limited.RegisterInternalCommandHandler<Reaction>(limitedReactions);
            limited.RegisterDomainEventHandler<Signal>(limitedReactions);
            limited.Seal();
            limited.PublishDomainEvent(new Signal("start"));
            SimulationRunner limitedRunner = new SimulationRunner(limited);
            Expect<InvalidOperationException>(() => limitedRunner.AdvanceTick());
            Check(string.Join(",", limitedTrace) == "event:start" && limitedRunner.Failure != null,
                "Cross-category reactions must respect the configured cycle budget.");
        }

        private static void RegisterPhases(SimulationPipeline pipeline, PhaseParticipant participant)
        {
            pipeline.RegisterPrePhysicsParticipant(participant);
            pipeline.RegisterPhysicsParticipant(participant);
            pipeline.RegisterPostPhysicsParticipant(participant);
            pipeline.RegisterStructuralCommitParticipant(participant);
            pipeline.RegisterPresentationParticipant(participant);
        }

        private readonly struct Input : IIntent { }
        private readonly struct Reaction : IInternalCommand
        {
            internal Reaction(string label) { Label = label; }
            internal string Label { get; }
        }
        private readonly struct Signal : IDomainEvent
        {
            internal Signal(string label) { Label = label; }
            internal string Label { get; }
        }
        private sealed class CallbackHandler : IIntentHandler<Input>
        {
            private readonly Action callback;
            internal CallbackHandler(Action callback) { this.callback = callback; }
            public void Handle(Input input) => callback();
        }
        private sealed class ReactionHandler : IInternalCommandHandler<Reaction>, IDomainEventHandler<Signal>
        {
            private readonly SimulationPipeline pipeline;
            private readonly List<string> observed;
            internal ReactionHandler(SimulationPipeline pipeline, List<string> observed)
            { this.pipeline = pipeline; this.observed = observed; }
            public void Handle(Reaction command)
            {
                observed.Add("command:" + command.Label);
                pipeline.PublishDomainEvent(new Signal(command.Label));
            }
            public void Handle(Signal domainEvent)
            {
                observed.Add("event:" + domainEvent.Label);
                if (domainEvent.Label == "start") pipeline.EnqueueInternalCommand(new Reaction("followup"));
            }
        }
        private sealed class PhaseParticipant : IPrePhysicsParticipant, IPhysicsParticipant,
            IPostPhysicsParticipant, IStructuralCommitParticipant, IPresentationParticipant
        {
            private readonly string name;
            private readonly SimulationPipeline pipeline;
            private readonly List<string> observed;
            internal PhaseParticipant(string name, SimulationPipeline pipeline, List<string> observed)
            { this.name = name; this.pipeline = pipeline; this.observed = observed; }
            void IPrePhysicsParticipant.Tick(SimulationContext context) => Produce("pre");
            public void Simulate(SimulationContext context) => Produce("physics");
            void IPostPhysicsParticipant.Tick(SimulationContext context) => Produce("post");
            public void Commit(SimulationContext context) => Produce("commit");
            public void CaptureTickState(SimulationContext context) => observed.Add("capture:" + name);
            public void Render(SimulationContext context, float alpha) => observed.Add("render:" + name);
            private void Produce(string phase)
            {
                string label = phase + ":" + name;
                observed.Add(label);
                pipeline.EnqueueInternalCommand(new Reaction(label));
            }
        }
        private sealed class FailingPresentation : IPresentationParticipant
        {
            public void CaptureTickState(SimulationContext context) { }
            public void Render(SimulationContext context, float alpha) => throw new ApplicationException("view failed");
        }
        private sealed class Counter
        {
            internal int Value;
        }
        private sealed class CounterObserver : ISimulationObserver<Counter, int>
        {
            public int Observe(Counter world) => world.Value;
        }
        private sealed class CounterDefinition : SimulationDefinition<Counter, float>
        {
            protected override void ValidateScenario(float scenario) { }
            protected override float GetTickDelta(float scenario) => scenario;
            protected override Counter CreateWorld(float scenario) => new Counter();
            protected override void Configure(SimulationBuilder builder, Counter world, float scenario)
                => builder.RegisterIntentHandler(new CallbackHandler(() => world.Value++));
            protected override void DestroyWorld(Counter world) { }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Expect<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("Expected " + typeof(TException).Name);
        }
    }
}
