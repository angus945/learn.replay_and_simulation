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
        private readonly bool usesDefaultLimits;
        private TemplateLimits limits;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private SimulationSession<TWorld, TScenario> core;
        private InvariantRegistry<TObservation> checks;
        private TraceRecorder trace;
        private readonly List<RecordedInput> inputs = new List<RecordedInput>();
        private readonly List<TemplateTick> ticks = new List<TemplateTick>();
        private readonly Dictionary<ulong, ActionResult> results = new Dictionary<ulong, ActionResult>();
        private readonly List<ActionResult> resultHistory = new List<ActionResult>();
        private readonly Dictionary<ulong, TemplateTraceMetadata> inputMetadata = new Dictionary<ulong, TemplateTraceMetadata>();
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
        private readonly SimulationDriveOwnership drive = new SimulationDriveOwnership();

        internal TestableSimulationSession(ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition,
            TScenario scenario, TemplateLimits limits)
        {
            this.definition = definition; this.limits = limits; usesDefaultLimits = limits == null;
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
        public string Policy => policy;
        public TemplateLimits Limits => limits;
        /// <summary>Live clock ownership, not a caller-provided session mode.</summary>
        public bool HasRealtimeDriver { get { EnsureIdle(); return drive.HasRealtimeDriver; } }
        public InvariantReport InvariantReport => report;
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
                TInput independent = definition.LoadInput(payload); // Describe only the frozen, decoded input.
                TemplateTraceMetadata metadata = definition.InputMetadata(independent);
                RecordedInput recorded = new RecordedInput(sequence, targetTick, payload);
                inputs.Add(recorded); sequences.Add(sequence); inputMetadata.Add(sequence, metadata);
                totalPayloadBytes += payloadBytes;
                if (!pending.TryGetValue(targetTick, out List<RecordedInput> batch))
                { batch = new List<RecordedInput>(); pending.Add(targetTick, batch); }
                batch.Add(recorded);
                trace.Record(new TraceEntry(Id, CurrentTick, sequence, "Admission", metadata.Type, "queue.accepted",
                    actor: metadata.Actor, target: metadata.Target));
                return new SubmissionResult(true, "queue.accepted");
            }
            catch (ArgumentException) { return new SubmissionResult(false, "input.invalid"); }
            finally { busy = false; }
        }

        public TemplateTick Step()
        {
            drive.EnsureManual(); return StepCore();
        }
        public RealtimeSimulationRunner CreateRealtimeRunner(int maxTicksPerFrame = 120,
            IRealtimeInputSource input = null, IRealtimePresentation presentation = null)
        {
            EnsureIdle();
            if (State != SessionState.Running) throw new InvalidOperationException("Session is not running.");
            return drive.CreateRunner(new TickSource(this), maxTicksPerFrame, input, presentation);
        }
        private sealed class TickSource : ISimulationTickSource
        {
            private readonly TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner;
            internal TickSource(TestableSimulationSession<TWorld, TScenario, TInput, TObservation> owner) { this.owner = owner; }
            public float TickDelta => owner.TickDelta;
            public ulong TickNumber => owner.CurrentTick;
            public bool PrepareTick() => owner.CanAdvanceRealtime();
            public void AdvanceTick() => owner.StepCore();
        }
        private bool CanAdvanceRealtime()
        {
            EnsureIdle();
            if (State == SessionState.Running && CurrentTick >= (ulong)limits.MaxTicks)
            { Stop(); cancellationReason = "tick.budget"; }
            return State == SessionState.Running;
        }
        private TemplateTick StepCore()
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
                        Context = new InputExecutionContext(Id, input.Sequence, target, null),
                        Metadata = inputMetadata[input.Sequence],
                        Begin = () => { executingSequence = input.Sequence; },
                        Complete = outcome =>
                        {
                            ActionResult result = new ActionResult(input.Sequence, target, outcome.Status, outcome.Code);
                            completed.Add(result); results.Add(result.Sequence, result); resultHistory.Add(result);
                            TemplateTraceMetadata metadata = inputMetadata[input.Sequence];
                            trace.Record(new TraceEntry(Id, target, input.Sequence, "Action", metadata.Type, outcome.Code,
                                actor: metadata.Actor, target: metadata.Target));
                            executingSequence = 0;
                        }
                    });
                }
                executingSequence = 0; core.Step();
                stage = "Observation"; observation = core.Observe(definition); observationTick = target;
                stage = "StateHash"; hash = definition.Hash(observation);
                trace.Record(new TraceEntry(Id, target, 0, "StateHash", "State", hash));
                stage = "Invariant";
                IReadOnlyList<InvariantViolation> violations = checks.Evaluate(observation);
                report = new InvariantReport(true, target, checks.Count, violations);
                foreach (InvariantViolation violation in violations)
                    trace.Record(new TraceEntry(Id, target, 0, "Invariant", violation.Code, violation.Detail));
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
                        completed.Add(result); results.Add(result.Sequence, result); resultHistory.Add(result);
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

        public TemplateActionResultPage Read(string sessionId, int afterIndex, int maxItems)
        {
            EnsureIdle();
            if (sessionId != Id) throw new ArgumentException("Result cursor belongs to a different session.", nameof(sessionId));
            if (afterIndex < 0 || afterIndex > resultHistory.Count) throw new ArgumentOutOfRangeException(nameof(afterIndex));
            if (maxItems < 1 || maxItems > 1024) throw new ArgumentOutOfRangeException(nameof(maxItems));
            int count = Math.Min(maxItems, resultHistory.Count - afterIndex);
            return new TemplateActionResultPage(resultHistory.GetRange(afterIndex, count), afterIndex + count,
                afterIndex + count < resultHistory.Count);
        }

        public void Stop()
        {
            EnsureIdle();
            core.Stop(); pending.Clear();
            if (State != SessionState.Faulted) { State = SessionState.Stopped; cancellationReason = "session.stopped"; }
        }
        public void Reset(TScenario scenario)
        {
            drive.EnsureManual();
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
            drive.EnsureManual();
            EnsureIdle(); busy = true; disposed = true;
            try { core.Dispose(); pending.Clear(); }
            finally { busy = false; }
        }

        private void Initialize(TScenario scenario)
        {
            if (ReferenceEquals(scenario, null)) throw new ArgumentNullException(nameof(scenario));
            TemplateLimits nextLimits = usesDefaultLimits ? definition.DefaultLimits(scenario) : limits;
            nextLimits.Validate();
            string payload = definition.SaveScenario(scenario); nextLimits.CheckPayload(payload);
            TScenario independent = definition.LoadScenario(payload);
            string nextPolicy = definition.PolicyId;
            if (string.IsNullOrWhiteSpace(nextPolicy)) throw new ArgumentException("PolicyId is required.");
            InvariantRegistry<TObservation> nextChecks = definition.CreateChecks();
            TraceRecorder nextTrace = new TraceRecorder(nextLimits.TraceCapacity);
            InvariantReport nextReport = new InvariantReport(false, 0, nextChecks.Count, Array.Empty<InvariantViolation>());
            string nextId = Guid.NewGuid().ToString("N");
            int nextPayloadBytes = System.Text.Encoding.UTF8.GetByteCount(payload);
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
            core = next; limits = nextLimits; checks = nextChecks; scenarioPayload = payload; initialHash = nextHash; policy = nextPolicy;
            observation = nextObservation; observationTick = 0; TickDelta = nextDelta;
            trace = nextTrace;
            inputs.Clear(); ticks.Clear(); results.Clear(); resultHistory.Clear(); inputMetadata.Clear(); sequences.Clear(); pending.Clear();
            totalPayloadBytes = nextPayloadBytes;
            Id = nextId; Failure = null; LastCompletedTick = 0; State = SessionState.Running;
            attemptedTick = 0;
            report = nextReport;
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
            TemplateTraceMetadata metadata = definition.DispatchMetadata(dispatch.Message);
            if (metadata == null)
            {
                trace.Record(new TraceEntry(Id, core.TickNumber, 0, dispatch.Category.ToString(), dispatch.Message.GetType().Name,
                    string.Empty, dispatch.Wave));
                return;
            }
            executingSequence = metadata.Sequence;
            trace.Record(new TraceEntry(Id, core.TickNumber, metadata.Sequence, dispatch.Category.ToString(), metadata.Type,
                metadata.Detail, dispatch.Wave, metadata.Actor, metadata.Target));
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
            public TemplateActionResultPage Read(string sessionId, int afterIndex, int maxItems) => owner.Read(sessionId, afterIndex, maxItems);
        }
    }
}
