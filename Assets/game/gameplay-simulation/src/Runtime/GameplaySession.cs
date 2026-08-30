using System;
using System.Collections.Generic;
using InvariantChecks;
using Testability;
using Testability.Templates;
using TraceBuffering;

namespace GameplaySimulation
{
    /// <summary>Compatibility ports and artifact projection over the shared runtime.
    /// New compositions use GameplayDefinition directly; no gameplay or scheduling rules live here.</summary>
    public sealed partial class GameplaySession : ITestSession<GameplayScenario>, IGameplayControl, IDisposable
    {
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly string policyRevision;
        private readonly List<Func<IInvariant<GameplayObservation>>> extraInvariants = new List<Func<IInvariant<GameplayObservation>>>();
        private readonly List<GameplayRequest> history = new List<GameplayRequest>();
        private readonly List<ActionResult> resultHistory = new List<ActionResult>();
        private readonly List<HashCheckpoint> hashHistory = new List<HashCheckpoint>();
        private TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> core;
        private GameplayScenario scenario;
        private SessionState initialState = SessionState.Created;
        private bool stepping, disposed;

        public GameplaySession(SimulationDriveMode driveMode = SimulationDriveMode.Manual, string policyRevision = "v1")
        {
            if (!Enum.IsDefined(typeof(SimulationDriveMode), driveMode)) throw new ArgumentOutOfRangeException(nameof(driveMode));
            if (string.IsNullOrWhiteSpace(policyRevision)) throw new ArgumentException("Policy revision is required.", nameof(policyRevision));
            this.policyRevision = policyRevision; DriveMode = driveMode;
            Diagnostics = new DiagnosticsPort(this);
            Gameplay = new GameplayPort(this); Simulation = new SimulationPort(this);
            Admin = new AdminPort(this); Results = new ResultsPort(this); Capabilities = new CapabilitiesPort(this);
        }
        public string Id => core == null ? string.Empty : core.Id;
        public SessionState State => core == null ? initialState : core.State;
        public ulong CurrentTick => core == null ? 0 : core.CurrentTick;
        public ulong LastCompletedTick => core == null ? 0 : core.LastCompletedTick;
        public FailureArtifact Failure { get; private set; }
        public string DiagnosticPolicy { get; private set; }
        public IDiagnosticReader<GameplayObservation> Diagnostics { get; }
        public IReadOnlyList<GameplayRequest> ActionHistory => new List<GameplayRequest>(history).AsReadOnly();
        public IReadOnlyList<HashCheckpoint> HashHistory => new List<HashCheckpoint>(hashHistory).AsReadOnly();
        public IReadOnlyList<TraceEntry> ReadTrace()
        { EnsureIdle(); return core == null ? Array.Empty<TraceEntry>() : core.CaptureRecording().Trace; }

        public void RegisterInvariant(Func<IInvariant<GameplayObservation>> factory)
        {
            EnsureIdle();
            if (State != SessionState.Created) throw new InvalidOperationException("Register invariants before Start.");
            extraInvariants.Add(factory ?? throw new ArgumentNullException(nameof(factory)));
        }
        public void Start(GameplayScenario initial)
        {
            EnsureIdle();
            if (State != SessionState.Created) throw new InvalidOperationException("Start is valid only for a new session.");
            Initialize(initial);
        }
        public void Reset(GameplayScenario initial) { EnsureIdle(); Initialize(initial); }
        public void Stop()
        {
            EnsureIdle();
            if (core == null) initialState = SessionState.Stopped;
            else core.Stop();
        }
        private void Initialize(GameplayScenario initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            initial.Validate();
            List<string> codes = new List<string> { "gameplay.valid_state" };
            List<Func<IInvariant<GameplayObservation>>> checks = new List<Func<IInvariant<GameplayObservation>>>();
            foreach (Func<IInvariant<GameplayObservation>> factory in extraInvariants)
            {
                IInvariant<GameplayObservation> check = factory() ?? throw new ArgumentException("Invariant factory returned null.");
                codes.Add(check.Code);
                checks.Add(() => check); // This definition creates exactly one session; Reset creates fresh checks.
            }
            codes.Sort(StringComparer.Ordinal);
            string nextPolicy = policyRevision + (initial.RandomRespawnDelay ? "/lifecycle-v3" : initial.ExtendedLifecycle ? "/lifecycle-v2" : "")
                + ":" + string.Join("|", codes);
            GameplayDefinition definition = new GameplayDefinition(checks, "compat/" + nextPolicy);
            TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> next = definition.CreateTestSession(initial);
            try { core?.Dispose(); }
            catch { next.Dispose(); throw; }
            core = next; scenario = initial; DiagnosticPolicy = nextPolicy; Failure = null;
            history.Clear(); resultHistory.Clear(); hashHistory.Clear();
            hashHistory.Add(new HashCheckpoint(0, GameplayStateHasher.Compute(core.Observe(), scenario)));
        }
        public SubmissionResult Submit(GameplayRequest request)
        {
            EnsureThread();
            if (State != SessionState.Running) return new SubmissionResult(false, "session.not_running");
            if (stepping) return new SubmissionResult(false, "session.busy");
            if (request == null) return new SubmissionResult(false, "request.null");
            SubmissionResult admitted = core.Submit(request.SessionId, request.Sequence, request.TargetTick,
                new GameplayInput(request.Kind, request.Actor, request.Target, request.X, request.Y));
            if (admitted.Queued) history.Add(request);
            string code = admitted.Code == "input.capacity" ? "action.capacity" : admitted.Code;
            if (code == "sequence.invalid_or_duplicate") code = request.Sequence == 0 ? "sequence.invalid" : "sequence.duplicate";
            return new SubmissionResult(admitted.Queued, code);
        }
        public TickReport Step()
        {
            if (DriveMode != SimulationDriveMode.Manual) throw new InvalidOperationException("Realtime driver owns this session clock.");
            return StepCore();
        }
        private TickReport StepCore()
        {
            EnsureIdle();
            if (core == null) throw new InvalidOperationException("Start the session first.");
            stepping = true;
            try
            {
                TemplateTick tick = core.Step();
                resultHistory.AddRange(tick.Results);
                // Preserve schema-1 artifact hashes without running a second simulation.
                string hash = tick.Hash == null ? string.Empty : GameplayStateHasher.Compute(core.Observe(), scenario);
                if (tick.Hash != null) hashHistory.Add(new HashCheckpoint(tick.Tick, hash));
                if (core.Failure != null && Failure == null)
                {
                    TemplateFailure failure = core.Failure;
                    TemplateRecording recording = core.CaptureRecording();
                    Failure = new FailureArtifact(Id, scenario, failure.Tick, failure.Sequence, failure.Code,
                        failure.ExceptionType == null ? null : failure.ExceptionType + ": " + failure.Detail,
                        history, resultHistory, hashHistory, recording.Trace, recording.DroppedTraceEntries,
                        core.Observe(), failure.ExceptionType, DiagnosticPolicy, failure.Stage, failure.LastCompletedTick);
                }
                InvariantReport report = core.InvariantReport;
                return new TickReport(tick.Tick, tick.Results, hash,
                    report.Tick == tick.Tick ? report.Violations : Array.Empty<InvariantViolation>());
            }
            finally { stepping = false; }
        }
        public GameplayObservation Observe()
        {
            EnsureIdle();
            if (core == null) throw new InvalidOperationException("Start the session before observing.");
            return core.Observe();
        }
        public LifecycleSnapshot ObserveLifecycle() => Observe().Lifecycle;
        public void Dispose()
        {
            if (disposed) return;
            EnsureIdle(); core?.Dispose(); disposed = true;
        }
        private void EnsureThread()
        {
            if (ownerThread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Use the session owner thread.");
            if (disposed) throw new ObjectDisposedException(nameof(GameplaySession));
        }
        private void EnsureIdle()
        { EnsureThread(); if (stepping) throw new InvalidOperationException("Read or change the session between ticks."); }
        private sealed class DiagnosticsPort : IDiagnosticReader<GameplayObservation>
        {
            private readonly GameplaySession owner;
            internal DiagnosticsPort(GameplaySession owner) { this.owner = owner; }
            public DiagnosticSnapshot<GameplayObservation> ObserveDiagnostics()
            {
                owner.EnsureIdle();
                if (owner.core == null) throw new InvalidOperationException("Start the session before reading diagnostics.");
                return owner.core.Diagnostics.ObserveDiagnostics();
            }
            public TraceBatch<TraceEntry> ReadTrace(TraceCursor cursor, int maxItems)
            {
                owner.EnsureIdle();
                if (owner.core == null) throw new InvalidOperationException("Start the session before reading trace.");
                return owner.core.Diagnostics.ReadTrace(cursor, maxItems);
            }
        }
    }
}
