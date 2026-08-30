using System;
using System.Collections.Generic;
using DeterministicSimulation.Framework;
using InvariantChecks;
using TraceBuffering;

namespace Testability.Templates
{
    /// <summary>Manual control/recording host. Input payloads are frozen at admission.
    /// Trusted definition callbacks execute on the owner thread, without rollback or watchdog.</summary>
    public sealed class TestableSimulationSession<TWorld, TScenario, TInput, TObservation> : IDisposable where TWorld : class
    {
        private readonly ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition;
        private readonly TemplateLimits limits;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private SimulationSession<TWorld, TScenario> core;
        private InvariantRegistry<TObservation> checks;
        private TraceRecorder trace;
        private readonly List<RecordedInput> inputs = new List<RecordedInput>();
        private readonly List<TemplateTick> ticks = new List<TemplateTick>();
        private readonly Dictionary<ulong, ActionResult> results = new Dictionary<ulong, ActionResult>();
        private readonly HashSet<ulong> sequences = new HashSet<ulong>();
        private readonly SortedDictionary<ulong, List<RecordedInput>> pending = new SortedDictionary<ulong, List<RecordedInput>>();
        private TObservation observation;
        private ulong observationTick;
        private InvariantReport report;
        private string scenarioPayload, initialHash, policy, stage, cancellationReason;
        private ulong executingSequence;
        private ulong attemptedTick;
        private long totalPayloadBytes;
        private bool busy, disposed;

        internal TestableSimulationSession(ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition,
            TScenario scenario, TemplateLimits limits)
        {
            this.definition = definition; this.limits = limits; limits.Validate();
            Diagnostics = new Reader(this);
            Gameplay = new GameplayPort(this); Simulation = new SimulationPort(this);
            Admin = new AdminPort(this); Results = new ResultsPort(this);
            busy = true;
            try { Initialize(scenario); }
            finally { busy = false; }
        }
        public string Id { get; private set; }
        public SessionState State { get; private set; }
        public ulong CurrentTick => attemptedTick;
        public ulong LastCompletedTick { get; private set; }
        public float TickDelta { get; private set; }
        public TemplateFailure Failure { get; private set; }
        public IDiagnosticReader<TObservation> Diagnostics { get; }
        public ITemplateGameplay<TInput, TObservation> Gameplay { get; }
        public ITemplateSimulation Simulation { get; }
        public ITemplateAdmin<TScenario> Admin { get; }
        public ITemplateResults Results { get; }
        public TObservation Observe() { EnsureIdle(); return observation; }

        public SubmissionResult Submit(string sessionId, ulong sequence, ulong targetTick, TInput input)
        {
            EnsureIdle();
            if (State != SessionState.Running) return new SubmissionResult(false, "session.not_running");
            if (sessionId != Id) return new SubmissionResult(false, "session.stale");
            if (sequence == 0 || sequences.Contains(sequence)) return new SubmissionResult(false, "sequence.invalid_or_duplicate");
            if (targetTick <= CurrentTick || targetTick > (ulong)limits.MaxTicks) return new SubmissionResult(false, "tick.out_of_range");
            if (inputs.Count >= limits.MaxInputs) return new SubmissionResult(false, "input.capacity");
            busy = true;
            try
            {
                string payload = definition.SaveInput(input);
                limits.CheckPayload(payload);
                int payloadBytes = System.Text.Encoding.UTF8.GetByteCount(payload);
                if (totalPayloadBytes + payloadBytes > limits.MaxTotalPayloadBytes) return new SubmissionResult(false, "input.payload_budget");
                definition.LoadInput(payload); // Validate codec before admitting immutable encoded input.
                RecordedInput recorded = new RecordedInput(sequence, targetTick, payload);
                inputs.Add(recorded); sequences.Add(sequence);
                totalPayloadBytes += payloadBytes;
                if (!pending.TryGetValue(targetTick, out List<RecordedInput> batch))
                { batch = new List<RecordedInput>(); pending.Add(targetTick, batch); }
                batch.Add(recorded);
                trace.Record(new TraceEntry(Id, CurrentTick, sequence, "Admission", "Input", "queue.accepted"));
                return new SubmissionResult(true, "queue.accepted");
            }
            catch (ArgumentException) { return new SubmissionResult(false, "input.invalid"); }
            finally { busy = false; }
        }

        public TemplateTick Step()
        {
            EnsureIdle();
            if (State != SessionState.Running) throw new InvalidOperationException("Session is not running.");
            if (CurrentTick >= (ulong)limits.MaxTicks)
            { Stop(); cancellationReason = "tick.budget"; throw new InvalidOperationException("Tick budget exhausted."); }
            busy = true;
            ulong target = CurrentTick + 1;
            attemptedTick = target;
            List<ActionResult> completed = new List<ActionResult>();
            List<RecordedInput> batch = pending.TryGetValue(target, out List<RecordedInput> queued) ? queued : new List<RecordedInput>();
            batch.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            pending.Remove(target);
            string hash = null;
            stage = "InputDecode"; executingSequence = 0;
            try
            {
                foreach (RecordedInput input in batch)
                {
                    executingSequence = input.Sequence;
                    core.EnqueueIntent(new ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation>.InputIntent
                    {
                        Input = definition.LoadInput(input.Payload),
                        Begin = () => { executingSequence = input.Sequence; },
                        Complete = outcome =>
                        {
                            ActionResult result = new ActionResult(input.Sequence, target, outcome.Status, outcome.Code);
                            completed.Add(result); results.Add(result.Sequence, result);
                            trace.Record(new TraceEntry(Id, target, input.Sequence, "Action", outcome.Status.ToString(), outcome.Code));
                            executingSequence = 0;
                        }
                    });
                }
                executingSequence = 0; core.Step();
                stage = "Observation"; observation = core.Observe(definition); observationTick = target;
                stage = "StateHash"; hash = definition.Hash(observation);
                stage = "Invariant";
                IReadOnlyList<InvariantViolation> violations = checks.Evaluate(observation);
                report = new InvariantReport(true, target, checks.Count, violations);
                if (violations.Count > 0) CaptureFailure(target, violations[0].Code, null, violations[0].Detail);
                else LastCompletedTick = target;
            }
            catch (Exception error)
            {
                foreach (RecordedInput input in batch)
                    if (!results.ContainsKey(input.Sequence))
                    {
                        ActionResult result = new ActionResult(input.Sequence, target, ActionStatus.Failed,
                            input.Sequence == executingSequence ? "simulation.exception" : "tick.aborted");
                        completed.Add(result); results.Add(result.Sequence, result);
                    }
                CaptureFailure(target, "simulation.exception", error, error.Message);
            }
            finally { busy = false; }
            completed.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            TemplateTick tick = new TemplateTick(target, hash, completed); ticks.Add(tick); return tick;
        }

        public TemplateActionLookup Find(string sessionId, ulong sequence)
        {
            EnsureIdle();
            if (sessionId != Id) return new TemplateActionLookup("StaleSession");
            if (results.TryGetValue(sequence, out ActionResult result)) return new TemplateActionLookup("Completed", result);
            if (!sequences.Contains(sequence)) return new TemplateActionLookup("Unknown");
            return new TemplateActionLookup(State == SessionState.Running ? "Pending" : "Cancelled", reason: cancellationReason);
        }

        public void Stop()
        {
            EnsureIdle();
            core.Stop(); pending.Clear();
            if (State != SessionState.Faulted) { State = SessionState.Stopped; cancellationReason = "session.stopped"; }
        }
        public void Reset(TScenario scenario)
        {
            EnsureIdle(); busy = true;
            try { Initialize(scenario); }
            finally { busy = false; }
        }
        public TemplateRecording CaptureRecording()
        {
            EnsureIdle();
            return new TemplateRecording(policy, Environment.Version + " / " + Environment.OSVersion, scenarioPayload, TickDelta,
                limits, initialHash, inputs, ticks, Failure, trace.Snapshot(), trace.DroppedCount);
        }
        public void Dispose()
        {
            if (disposed) return;
            EnsureIdle(); busy = true; disposed = true;
            try { core.Dispose(); pending.Clear(); }
            finally { busy = false; }
        }

        private void Initialize(TScenario scenario)
        {
            string payload = definition.SaveScenario(scenario); limits.CheckPayload(payload);
            TScenario independent = definition.LoadScenario(payload);
            string nextPolicy = definition.PolicyId;
            if (string.IsNullOrWhiteSpace(nextPolicy)) throw new ArgumentException("PolicyId is required.");
            InvariantRegistry<TObservation> nextChecks = definition.CreateChecks();
            SimulationSession<TWorld, TScenario> next = definition.CreateSession(independent, RecordPhase, RecordDispatch);
            TObservation nextObservation; string nextHash; float nextDelta;
            try
            {
                nextObservation = next.Observe(definition); nextHash = definition.Hash(nextObservation);
                nextDelta = definition.TickDelta(independent);
            }
            catch (Exception setupError)
            {
                try { next.Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
            try { core?.Dispose(); }
            catch (Exception oldCleanupError)
            {
                disposed = true; // Do not expose a half-reset session.
                try { next.Dispose(); }
                catch (Exception newCleanupError) { throw new AggregateException(oldCleanupError, newCleanupError); }
                throw;
            }
            core = next; checks = nextChecks; scenarioPayload = payload; initialHash = nextHash; policy = nextPolicy;
            observation = nextObservation; observationTick = 0; TickDelta = nextDelta;
            trace = new TraceRecorder(limits.TraceCapacity);
            inputs.Clear(); ticks.Clear(); results.Clear(); sequences.Clear(); pending.Clear();
            totalPayloadBytes = System.Text.Encoding.UTF8.GetByteCount(payload);
            Id = Guid.NewGuid().ToString("N"); Failure = null; LastCompletedTick = 0; State = SessionState.Running;
            attemptedTick = 0;
            report = new InvariantReport(false, 0, checks.Count, Array.Empty<InvariantViolation>());
            stage = "Initialize"; executingSequence = 0; cancellationReason = null;
        }
        private void CaptureFailure(ulong tick, string code, Exception error, string detail)
        {
            if (Failure != null) return;
            Failure = new TemplateFailure(tick, LastCompletedTick, executingSequence, stage, code, error?.GetType().FullName, detail);
            State = SessionState.Faulted; cancellationReason = "session.faulted"; pending.Clear(); core.Stop();
            trace.Record(new TraceEntry(Id, tick, executingSequence, stage, "Failure", code));
        }
        private void RecordPhase(SimulationPhase phase, bool entering)
        {
            stage = phase.ToString(); executingSequence = 0;
            trace.Record(new TraceEntry(Id, core.TickNumber, 0, "Phase", stage, entering ? "begin" : "end"));
        }
        private void RecordDispatch(MessageDispatch dispatch)
        {
            executingSequence = 0;
            trace.Record(new TraceEntry(Id, core.TickNumber, 0, stage, dispatch.Category.ToString(), dispatch.Message.GetType().Name, dispatch.Wave));
        }
        private void EnsureIdle()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Use the session owner thread.");
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (busy) throw new InvalidOperationException("Session is busy; callbacks cannot reenter.");
        }
        private sealed class Reader : IDiagnosticReader<TObservation>
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal Reader(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public DiagnosticSnapshot<TObservation> ObserveDiagnostics()
            {
                owner.EnsureIdle();
                return new DiagnosticSnapshot<TObservation>(owner.Id, owner.State, owner.CurrentTick, owner.observation, owner.report, owner.Failure?.Code, owner.observationTick);
            }
            public TraceBatch<TraceEntry> ReadTrace(TraceCursor cursor, int maxItems)
            { owner.EnsureIdle(); return owner.trace.Reader.Read(cursor, maxItems); }
        }
        private sealed class GameplayPort : ITemplateGameplay<TInput, TObservation>
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal GameplayPort(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public string Id => owner.Id;
            public ulong CurrentTick => owner.CurrentTick;
            public SubmissionResult Submit(string sessionId, ulong sequence, ulong targetTick, TInput input) => owner.Submit(sessionId, sequence, targetTick, input);
            public TObservation Observe() => owner.Observe();
        }
        private sealed class SimulationPort : ITemplateSimulation
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal SimulationPort(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public TemplateTick Step() => owner.Step();
        }
        private sealed class AdminPort : ITemplateAdmin<TScenario>
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal AdminPort(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public void Reset(TScenario scenario) => owner.Reset(scenario);
            public void Stop() => owner.Stop();
        }
        private sealed class ResultsPort : ITemplateResults
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal ResultsPort(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public TemplateActionLookup Find(string sessionId, ulong sequence) => owner.Find(sessionId, sequence);
        }
    }
}
