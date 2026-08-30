using System;
using System.IO;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using InvariantChecks;
using TraceBuffering;
using Testability.Templates;

namespace Testability.Tests
{
    // Pure domain: does not implement any simulation interface.
    public sealed class TemplateCounter
    {
        public TemplateCounter(int initial) { Value = initial; }
        public int Value { get; private set; }
        public void Set(int value) { Value = value; }
    }
    public sealed class CounterInput { public int Amount; }
    public sealed class CounterSnapshot
    {
        public CounterSnapshot(int value) { Value = value; }
        public int Value { get; }
    }

    // Executable reference definition. Counter is intentionally unrelated to the game's movement/combat model.
    public sealed class ReplayCounterDefinition : ReplayableSimulationDefinition<TemplateCounter, int, CounterInput, CounterSnapshot>
    {
        public int CleanupCount;
        public string Policy = "counter-v1";
        public bool BrokenObservation;
        public bool BrokenHash;
        public bool BrokenPhase;
        public Action DuringExecute;
        public override string PolicyId => Policy;
        protected override void ValidateScenario(int scenario)
        { if (scenario < 0 || scenario > 100) throw new ArgumentOutOfRangeException(nameof(scenario)); }
        protected override float GetTickDelta(int scenario) => .25f;
        protected override TemplateCounter CreateWorld(int scenario) => new TemplateCounter(scenario);
        protected override void DestroyWorld(TemplateCounter world) { CleanupCount++; }
        protected override void ConfigureWorld(SimulationBuilder builder, TemplateCounter world, int scenario)
        { if (BrokenPhase) builder.RegisterPrePhysicsParticipant(new FailingPhase()); }
        private sealed class FailingPhase : IPrePhysicsParticipant
        {
            public void Tick(SimulationContext context) => throw new InvalidOperationException("phase failure");
        }
        protected override string EncodeScenario(int scenario) => scenario.ToString(CultureInfo.InvariantCulture);
        protected override int DecodeScenario(string payload) => int.Parse(payload, CultureInfo.InvariantCulture);
        protected override string EncodeInput(CounterInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return input.Amount.ToString(CultureInfo.InvariantCulture);
        }
        protected override CounterInput DecodeInput(string payload) => new CounterInput { Amount = int.Parse(payload, CultureInfo.InvariantCulture) };
        protected override CounterSnapshot CaptureObservation(TemplateCounter world)
        {
            if (BrokenObservation && world.Value == 77) throw new InvalidOperationException("observation failed");
            return new CounterSnapshot(world.Value);
        }
        protected override byte[] EncodeCanonicalState(CounterSnapshot observation)
        {
            if (BrokenHash && observation.Value == 77) throw new InvalidOperationException("hash failed");
            return Encoding.UTF8.GetBytes("counter-state-v1:" + observation.Value.ToString(CultureInfo.InvariantCulture));
        }
        protected override void ConfigureInvariants(InvariantRegistry<CounterSnapshot> invariants) => invariants.Register(new Bounds());
        protected override InputOutcome ExecuteInput(TemplateCounter world, CounterInput input, IDomainEventSink events)
        {
            DuringExecute?.Invoke();
            if (input.Amount < 0) return new InputOutcome(ActionStatus.Rejected, "amount.negative");
            world.Set(input.Amount);
            if (input.Amount == 999) throw new InvalidOperationException("injected after mutation");
            return new InputOutcome(ActionStatus.Accepted, "counter.set");
        }
        private sealed class Bounds : IInvariant<CounterSnapshot>
        {
            public string Code => "counter.bounds";
            public InvariantViolation Evaluate(CounterSnapshot state) => state.Value > 100
                ? new InvariantViolation(Code, "Counter exceeds 100.") : null;
        }
    }

    public static class TemplateContractChecks
    {
        public static void RealtimeRecordingAndOwnership()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session =
                definition.CreateTestSession(0, new TemplateLimits(maxTicks: 4)))
            {
                using (RealtimeSimulationRunner runner = session.CreateRealtimeRunner(input: new CounterInputSource(session, 1)))
                {
                    Expect<InvalidOperationException>(() => session.Simulation.Step());
                    Expect<InvalidOperationException>(() => session.Step());
                    Expect<InvalidOperationException>(() => session.CreateRealtimeRunner());
                    Expect<InvalidOperationException>(() => session.Admin.Reset(0));
                    Expect<InvalidOperationException>(() => session.Dispose());
                    Check(runner.AdvanceTime(5) == 4 && session.CurrentTick == 4 && session.State == SessionState.Stopped,
                        "Realtime tick budget did not stop cleanly.");
                    Check(session.Diagnostics.ObserveDiagnostics().Tick == 4, "Diagnostics not updated by realtime ticks.");
                    using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(session.CaptureRecording()))
                    {
                        replay.Play(); replay.AdvanceTime(1);
                        Check(replay.State == TemplateReplayState.Completed, "Realtime recording did not replay.");
                    }
                }
                session.Admin.Reset(0);
                session.Step();
                using (RealtimeSimulationRunner runner = session.CreateRealtimeRunner(input: new CounterInputSource(session, 77)))
                {
                    definition.BrokenObservation = true;
                    Check(runner.AdvanceTime(2) == 1 && session.State == SessionState.Faulted && session.CurrentTick == 2,
                        "Recorded fault did not stop catch-up.");
                    Check(runner.AdvanceTime(1) == 0 && session.Failure != null, "Fault lost evidence or kept advancing.");
                }
            }
        }
        private sealed class CounterInputSource : IRealtimeInputSource
        {
            private readonly TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session;
            private readonly int amount;
            internal CounterInputSource(TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session, int amount)
            { this.session = session; this.amount = amount; }
            public void AcquireInput(SimulationTick tick)
            {
                Check(session.Gameplay.Submit(session.Id, tick.Number, tick.Number, new CounterInput { Amount = amount }).Queued,
                    "Realtime input admission failed.");
            }
        }
        public static void AdmissionAndDiagnostics()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            {
                CounterInput mutable = new CounterInput { Amount = 5 };
                Check(session.Gameplay.Submit(session.Id, 1, 2, mutable).Queued, "Input admission"); mutable.Amount = 9;
                CounterSnapshot before = session.Observe();
                Check(session.Results.Find(session.Id, 1).State == "Pending", "Pending lookup");
                Check(session.Step().Results.Count == 0, "Premature execution");
                Check(session.Step().Results[0].Status == ActionStatus.Accepted && session.Observe().Value == 5, "Frozen input");
                Check(before.Value == 0, "Mutable snapshot");
                TraceBatch<TraceEntry> trace = session.Diagnostics.ReadTrace(default(TraceCursor), 1000);
                session.Diagnostics.ObserveDiagnostics(); session.Diagnostics.ObserveDiagnostics();
                Check(session.Diagnostics.ReadTrace(trace.NextCursor, 1000).Items.Count == 0 && session.CurrentTick == 2, "Observation side effect");
                Check(!(session.Diagnostics is ITemplateSimulation) && !(session.Gameplay is ITemplateAdmin<int>), "Facade escape");
                Check(session.Diagnostics.ObserveDiagnostics().Invariants.Evaluated, "No invariant report");
            }
        }
        public static void OrderingResetAndLimits()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0, new TemplateLimits(3, 3, 2)))
            {
                string oldId = session.Id;
                session.Submit(session.Id, 2, 1, new CounterInput { Amount = 8 });
                session.Submit(session.Id, 1, 1, new CounterInput { Amount = 3 });
                Check(!session.Submit(session.Id, 1, 2, new CounterInput()).Queued, "Duplicate sequence");
                Check(!session.Submit("stale", 9, 1, new CounterInput()).Queued, "Stale admission");
                Check(!session.Submit(session.Id, 9, 0, new CounterInput()).Queued, "Past tick");
                session.Step(); Check(session.Observe().Value == 8, "Not sorted by sequence");
                TraceBatch<TraceEntry> old = session.Diagnostics.ReadTrace(default(TraceCursor), 10);
                Check(old.MissedCount > 0, "Trace gap not visible");
                session.Submit(session.Id, 3, 3, new CounterInput());
                Check(!session.Submit(session.Id, 4, 3, new CounterInput()).Queued, "Input bound");
                session.Stop(); Check(session.Find(session.Id, 3).CancellationReason == "session.stopped", "Stop cancellation");
                Expect<InvalidOperationException>(() => session.Step());
                Expect<ArgumentOutOfRangeException>(() => session.Reset(-1));
                Check(session.Id == oldId && session.Observe().Value == 8, "Invalid Reset mutated world");
                session.Reset(2);
                Check(session.Id != oldId && session.Find(oldId, 1).State == "StaleSession" && session.Observe().Value == 2, "Reset identity/state");
                Check(session.Diagnostics.ReadTrace(old.NextCursor, 10).StreamChanged, "Trace stream identity");
                session.Step(); session.Step(); session.Step();
                Expect<InvalidOperationException>(() => session.Step()); Check(session.State == SessionState.Stopped, "Tick budget stop");
            }
        }
        public static void ReplayFrameMatrix()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            TemplateRecording recording;
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            {
                session.Submit(session.Id, 2, 1, new CounterInput { Amount = 8 });
                session.Submit(session.Id, 1, 1, new CounterInput { Amount = 3 });
                session.Submit(session.Id, 3, 3, new CounterInput { Amount = -1 });
                session.Submit(session.Id, 4, 9, new CounterInput { Amount = 42 }); // Admitted beyond recording end.
                for (int i = 0; i < 6; i++) session.Step();
                recording = RoundTrip(session.CaptureRecording());
            }
            foreach (float delta in new float[] { 1f / 30, 1f / 60, 1f / 144, .37f })
            using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(recording))
            {
                replay.Play(); replay.Pause(); Check(replay.CurrentTick == 0, "Pause moved clock");
                replay.Step(); replay.Play();
                for (int i = 0; i < 10000 && replay.State == TemplateReplayState.Playing; i++) replay.AdvanceTime(delta);
                Check(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null && replay.Observe().Value == 8, "Replay matrix mismatch");
                replay.Restart(); Check(replay.CurrentTick == 0, "Restart failed");
            }
        }
        public static void FailureReplay()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            {
                session.Step();
                session.Submit(session.Id, 1, 2, new CounterInput { Amount = 999 });
                session.Submit(session.Id, 2, 2, new CounterInput());
                session.Submit(session.Id, 3, 3, new CounterInput()); session.Step();
                TemplateFailure failure = session.Failure;
                Check(failure.Sequence == 1 && failure.Stage == "IntentHandling" && failure.LastCompletedTick == 1, "Fault evidence");
                Check(session.Find(session.Id, 2).Result.Code == "tick.aborted", "Aborted action");
                Check(session.Find(session.Id, 3).CancellationReason == "session.faulted", "Future cancellation");
                Check(session.Diagnostics.ObserveDiagnostics().ObservationTick == 1, "Stale snapshot tick");
                session.Stop(); Expect<InvalidOperationException>(() => session.Step());
                Check(ReferenceEquals(failure, session.Failure), "First evidence replaced");
                using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(RoundTrip(session.CaptureRecording())))
                {
                    replay.Step(); replay.Step(); Check(replay.State == TemplateReplayState.ReproducedFailure, "Failure did not reproduce");
                }
                session.Reset(0); Check(session.Failure == null, "Reset retained failure");
            }
        }
        public static void InvariantAndCaptureFailures()
        {
            foreach (int mode in new int[] { 0, 1, 2 })
            {
                ReplayCounterDefinition definition = new ReplayCounterDefinition { BrokenObservation = mode == 1, BrokenHash = mode == 2 };
                using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
                {
                    session.Submit(session.Id, 1, 1, new CounterInput { Amount = mode == 0 ? 101 : 77 }); session.Step();
                    Check(session.Failure.Stage == (mode == 0 ? "Invariant" : mode == 1 ? "Observation" : "StateHash"), "Capture stage");
                    Check(session.LastCompletedTick == 0, "Failed tick marked complete");
                    using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(RoundTrip(session.CaptureRecording())))
                    { replay.Step(); Check(replay.State == TemplateReplayState.ReproducedFailure, "Capture failure mismatch"); }
                }
            }
        }
        public static void DivergenceAndMalformedRecording()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            TemplateRecording saved;
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            { session.Step(); saved = session.CaptureRecording(); }
            definition.Policy = "counter-v2";
            using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(saved))
                Check(replay.State == TemplateReplayState.Diverged && replay.FirstDifference.Category == "policy", "Policy mismatch ignored");
            definition.Policy = saved.Policy;
            TemplateRecording altered = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, new[] { new TemplateTick(1, "wrong", Array.Empty<ActionResult>()) }, null, saved.Trace, saved.DroppedTraceEntries);
            using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(altered))
            { replay.Step(); Check(replay.FirstDifference.Category == "state_hash", "Hash mismatch ignored"); }
            TemplateRecording malformed = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, new[] { new TemplateTick(2, "wrong", Array.Empty<ActionResult>()) }, null, saved.Trace, 0);
            Expect<ArgumentException>(() => definition.CreateReplay(malformed));
        }
        public static void ThreadAndReentry()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition();
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            {
                Task.Run(() => Expect<InvalidOperationException>(() => session.Observe())).GetAwaiter().GetResult();
                definition.DuringExecute = () => session.Step();
                session.Submit(session.Id, 1, 1, new CounterInput()); session.Step();
                Check(session.State == SessionState.Faulted, "Reentry allowed");
            }
        }
        public static void PhaseAndFileBounds()
        {
            ReplayCounterDefinition definition = new ReplayCounterDefinition { BrokenPhase = true };
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0))
            {
                session.Submit(session.Id, 1, 1, new CounterInput { Amount = 3 }); session.Step();
                Check(session.Failure.Stage == "PrePhysics" && session.Failure.Sequence == 0, "Phase blamed last action");
                using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(RoundTrip(session.CaptureRecording())))
                { replay.Step(); Check(replay.State == TemplateReplayState.ReproducedFailure, "Phase failure mismatch"); }
                using (MemoryStream bytes = new MemoryStream())
                {
                    TemplateRecordingIO.Write(bytes, session.CaptureRecording());
                    bytes.Position = 0; Expect<ArgumentException>(() => TemplateRecordingIO.Read(bytes, 1));
                    Check(bytes.CanRead, "Reader closed caller stream");
                }
            }
            definition.BrokenPhase = false;
            using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session = definition.CreateTestSession(0,
                new TemplateLimits(maxPayloadBytes: 2, maxTotalPayloadBytes: 2)))
            {
                Check(session.Submit(session.Id, 1, 1, new CounterInput { Amount = 3 }).Queued, "Small payload admission");
                Check(session.Submit(session.Id, 2, 1, new CounterInput { Amount = 4 }).Code == "input.payload_budget", "Aggregate byte bound");
                Check(!session.Submit(session.Id, 2, 1, null).Queued, "Null input admission");
            }
        }
        private static TemplateRecording RoundTrip(TemplateRecording recording)
        {
            using (MemoryStream stream = new MemoryStream())
            { TemplateRecordingIO.Write(stream, recording); stream.Position = 0; return TemplateRecordingIO.Read(stream); }
        }
        private static void Check(bool value, string detail) { if (!value) throw new Exception(detail); }
        private static T Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T error) { return error; } throw new Exception("Expected " + typeof(T).Name); }
    }
}
