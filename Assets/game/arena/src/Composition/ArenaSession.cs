using System;
using System.Collections.Generic;
using System.Text;
using Arena.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using Module.Verification.Diagnostics;
using Module.Verification.RuntimeControl;
using Module.Verification.StateSnapshot;
using Module.Verification.Evidence;
using Module.Verification.Oracle;
using Module.Verification.TraceBuffer;

namespace Arena.Composition
{
    /// <summary>Arena-owned host workflow. The general modules do not know or choose its next step.</summary>
    public sealed class ArenaSession : IDisposable, IArenaInputExecutionObserver
    {
        private sealed class Completion
        {
            public Completion(ArenaQueuedOperation operation, ArenaOperationResult outcome)
            {
                Operation = operation;
                Outcome = outcome;
            }

            public ArenaQueuedOperation Operation { get; }
            public ArenaOperationResult Outcome { get; }
        }

        private sealed class TickSource : ISimulationTickSource
        {
            private readonly ArenaSession owner;

            public TickSource(ArenaSession owner)
            {
                this.owner = owner;
            }

            public float TickDelta
            {
                get { return owner.TickDelta; }
            }

            public ulong TickNumber
            {
                get { return owner.CurrentTick; }
            }

            public bool PrepareTick()
            {
                return owner.CanAdvanceRealtime();
            }

            public void AdvanceTick()
            {
                owner.StepCore();
            }
        }

        private sealed class DiagnosticReader : IArenaDiagnosticReader
        {
            private readonly ArenaSession owner;

            public DiagnosticReader(ArenaSession owner)
            {
                this.owner = owner;
            }

            public ArenaDiagnosticSnapshot ReadSnapshot()
            {
                owner.EnsureIdle();
                return owner.latestDiagnostics;
            }

            public TraceBatch<ArenaTraceEntry> ReadTrace(TraceCursor cursor, int maxItems)
            {
                owner.EnsureIdle();
                return owner.trace.Reader.Read(cursor, maxItems);
            }
        }

        private sealed class OperationReader : IArenaOperationReader
        {
            private readonly ArenaSession owner;

            public OperationReader(ArenaSession owner)
            {
                this.owner = owner;
            }

            public OperationRead<ArenaOperationResult> Find(OperationHandle handle)
            {
                owner.EnsureIdle();
                return owner.controls.Find(handle);
            }

            public ArenaOperationResultPage Read(int afterIndex, int maxItems)
            {
                owner.EnsureIdle();
                return owner.controls.Read(afterIndex, maxItems);
            }
        }

        private readonly ArenaDefinition definition;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly SimulationDriveOwnership drive = new SimulationDriveOwnership();
        private readonly List<ArenaRecordedTick> ticks = new List<ArenaRecordedTick>();
        private readonly List<Completion> completions = new List<Completion>();
        private readonly Dictionary<long, ArenaQueuedOperation> executingOperations = new Dictionary<long, ArenaQueuedOperation>();
        private SimulationSession<ArenaRuntime, ArenaScenario> core;
        private ArenaControlAdapter controls;
        private ArenaObservationAdapter observations;
        private OracleSet<ArenaObservation> oracles;
        private TraceBuffer<ArenaTraceEntry> trace;
        private CollectingDiagnosticSink diagnosticSink;
        private ArenaScenario scenario;
        private string scenarioPayload;
        private string initialDigest;
        private string stage;
        private long executingSequence;
        private ulong attemptedTick;
        private long epoch;
        private bool busy;
        private bool disposed;
        private ArenaObservation observation;
        private StateSnapshotReference observationReference;
        private EvaluationReport evaluation;
        private ArenaDiagnosticSnapshot latestDiagnostics;

        internal ArenaSession(ArenaDefinition definition, ArenaScenario scenario, ArenaLimits limits)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Limits = limits ?? definition.CreateLimits(scenario);
            Limits.Validate();
            Diagnostics = new DiagnosticReader(this);
            Controls = new OperationReader(this);
            busy = true;
            try
            {
                Initialize(scenario);
            }
            finally
            {
                busy = false;
            }
        }

        public string Id { get; private set; }
        public ArenaSessionState State { get; private set; }
        public ulong CurrentTick
        {
            get { return attemptedTick; }
        }
        public ulong LastCompletedTick { get; private set; }
        public float TickDelta
        {
            get { return scenario.TickDelta; }
        }
        public ArenaFailure Failure { get; private set; }
        public ArenaLimits Limits { get; }
        public string PolicyId
        {
            get { return definition.PolicyId; }
        }
        public string InitialDigest
        {
            get { return initialDigest; }
        }
        public IArenaDiagnosticReader Diagnostics { get; }
        public IStateSnapshotReader<ArenaObservation> ObservationReader
        {
            get { return observations.Reader; }
        }
        public IArenaOperationReader Controls { get; }
        public bool HasRealtimeDriver
        {
            get
            {
                EnsureIdle();
                return drive.HasRealtimeDriver;
            }
        }

        public ArenaObservation Observe()
        {
            EnsureIdle();
            return observation;
        }

        public OperationAdmission Submit(ArenaInput input, ulong targetTick)
        {
            EnsureIdle();
            if (State != ArenaSessionState.Running) return OperationAdmission.Denied("session.not_running");
            OperationAdmission admission = controls.Submit(CurrentTick, targetTick, input);
            if (admission.IsAdmitted)
            {
                ArenaTraceMetadata metadata = definition.DescribeInput(input);
                RecordTrace(new ArenaTraceEntry(Id, CurrentTick, admission.Handle.Sequence, "Admission", metadata.Type, admission.Code, actor: metadata.Actor, target: metadata.Target));
            }
            return admission;
        }

        public ArenaTickEvidence Step()
        {
            drive.EnsureManual();
            return StepCore();
        }

        public RealtimeSimulationRunner CreateRealtimeRunner(int maxTicksPerFrame = 120, IRealtimeInputSource input = null, IRealtimePresentation presentation = null)
        {
            EnsureIdle();
            if (State != ArenaSessionState.Running) throw new InvalidOperationException("Arena session is not running.");
            return drive.CreateRunner(new TickSource(this), maxTicksPerFrame, input, presentation);
        }

        public ArenaRecording CaptureRecording()
        {
            EnsureIdle();
            TraceBatch<ArenaTraceEntry> traceBatch = trace.Reader.Read(default(TraceCursor), trace.Capacity);
            List<ArenaTraceEntry> traceEntries = new List<ArenaTraceEntry>();
            foreach (TraceRecord<ArenaTraceEntry> record in traceBatch.Items) traceEntries.Add(record.Entry);
            return new ArenaRecording(definition.PolicyId, Environment.Version + " / " + Environment.OSVersion, scenarioPayload, TickDelta, Limits, initialDigest, controls.Inputs, ticks, traceEntries, trace.OverwrittenCount);
        }

        public EvidenceBundle CaptureEvidence(string runId, string caseId)
        {
            EnsureIdle();
            return ArenaEvidenceFactory.Build(runId, caseId, CaptureRecording(), latestDiagnostics);
        }

        public void Stop()
        {
            EnsureIdle();
            core.Stop();
            controls.CancelPending("session.stopped");
            if (State != ArenaSessionState.Faulted) State = ArenaSessionState.Stopped;
            UpdateDiagnosticSnapshot();
        }

        public void Dispose()
        {
            if (disposed) return;
            drive.EnsureManual();
            EnsureIdle();
            disposed = true;
            controls.CancelPending("session.disposed");
            core.Dispose();
        }

        void IArenaInputExecutionObserver.OnInputExecutionStarted(ArenaInputIntent intent)
        {
            executingSequence = intent.Context.Handle.Sequence;
        }

        void IArenaInputExecutionObserver.OnInputExecutionCompleted(ArenaInputIntent intent, ArenaOperationResult outcome)
        {
            ArenaQueuedOperation operation = FindCurrentOperation(intent.Context.Handle.Sequence);
            completions.Add(new Completion(operation, outcome));
            executingSequence = 0;
        }

        private void Initialize(ArenaScenario nextScenario)
        {
            if (nextScenario == null) throw new ArgumentNullException(nameof(nextScenario));
            nextScenario.Validate();
            string nextPayload = definition.EncodeScenario(nextScenario);
            Limits.CheckPayload(nextPayload);
            ArenaScenario independent = definition.DecodeScenario(nextPayload);
            string nextId = Guid.NewGuid().ToString("N");
            epoch = checked(epoch + 1);
            ArenaObservationAdapter nextObservations = new ArenaObservationAdapter();
            TraceBuffer<ArenaTraceEntry> nextTrace = new TraceBuffer<ArenaTraceEntry>(Limits.TraceCapacity);
            CollectingDiagnosticSink nextDiagnostics = new CollectingDiagnosticSink(Limits.TraceCapacity);
            ArenaControlAdapter nextControls = new ArenaControlAdapter(definition, Limits, nextId, epoch, Encoding.UTF8.GetByteCount(nextPayload));
            OracleSet<ArenaObservation> nextOracles = definition.CreateOracleSet();
            Id = nextId;
            observations = nextObservations;
            trace = nextTrace;
            diagnosticSink = nextDiagnostics;
            controls = nextControls;
            oracles = nextOracles;
            core = definition.CreateCoreSession(independent, RecordPhase, RecordDispatch);
            scenario = independent;
            scenarioPayload = nextPayload;
            observationReference = observations.Publish(core, Id, epoch);
            StateSnapshotRead<ArenaObservation> initialRead = observations.Reader.Read(observationReference);
            observation = initialRead.Snapshot;
            initialDigest = ArenaStateDigest.Compute(observation);
            evaluation = oracles.Evaluate("tick:0", observation);
            ticks.Clear();
            Failure = null;
            attemptedTick = 0;
            LastCompletedTick = 0;
            State = ArenaSessionState.Running;
            stage = "Initialize";
            executingSequence = 0;
            UpdateDiagnosticSnapshot();
        }

        private bool CanAdvanceRealtime()
        {
            EnsureIdle();
            if (State == ArenaSessionState.Running && CurrentTick >= (ulong)Limits.MaxTicks) Stop();
            return State == ArenaSessionState.Running;
        }

        private ArenaTickEvidence StepCore()
        {
            EnsureIdle();
            if (State != ArenaSessionState.Running) throw new InvalidOperationException("Arena session is not running.");
            if (CurrentTick >= (ulong)Limits.MaxTicks)
            {
                Stop();
                throw new InvalidOperationException("Arena tick budget exhausted.");
            }
            busy = true;
            try
            {
                ulong target = CurrentTick + 1;
                attemptedTick = target;
                completions.Clear();
                executingOperations.Clear();
                IReadOnlyList<ArenaQueuedOperation> batch = controls.TakeBatch(target);
                string digest = null;
                StateSnapshotReference barrier = default(StateSnapshotReference);
                try
                {
                    stage = "InputDecode";
                    foreach (ArenaQueuedOperation operation in batch)
                    {
                        executingOperations.Add(operation.Handle.Sequence, operation);
                        controls.MarkRunning(operation);
                        ArenaInput input = definition.DecodeInput(operation.Payload);
                        ArenaInputExecutionContext context = new ArenaInputExecutionContext(Id, operation.Handle, target, null);
                        ArenaInputIntent intent = new ArenaInputIntent(input, context, operation.Metadata, this);
                        core.EnqueueIntent(intent);
                    }
                    executingSequence = 0;
                    core.Step();
                    stage = "Observation";
                    barrier = observations.Publish(core, Id, epoch);
                    StateSnapshotRead<ArenaObservation> read = observations.Reader.Read(barrier);
                    observation = read.Snapshot;
                    observationReference = barrier;
                    stage = "StateDigest";
                    digest = ArenaStateDigest.Compute(observation);
                    RecordTrace(new ArenaTraceEntry(Id, target, 0, "StateDigest", "State", digest));
                    stage = "Oracle";
                    evaluation = oracles.Evaluate("tick:" + target, observation);
                    RecordOracleResults(target, evaluation);
                    if (evaluation.Verdict == TestVerdict.Failed || evaluation.Verdict == TestVerdict.InfrastructureError)
                    {
                        OracleResult result = FindFailedOracleResult(evaluation);
                        string code = result == null ? "oracle.infrastructure" : result.Code;
                        string detail = result == null ? "Oracle evaluation failed." : result.Detail;
                        StateSnapshotCaptureFailure(target, code, null, detail);
                    }
                    else
                    {
                        LastCompletedTick = target;
                    }
                }
                catch (Exception exception)
                {
                    observations.ReportFailure(Id, epoch, "simulation.exception", exception.Message);
                    StateSnapshotCaptureFailure(target, "simulation.exception", exception, exception.Message);
                }
                List<ArenaOperationResult> completed = CompleteBatch(batch, barrier);
                ArenaTickEvidence evidence = new ArenaTickEvidence(0, target, digest, completed, evaluation, Failure);
                ticks.Add(new ArenaRecordedTick(target, digest, completed, ArenaRecordedFailure.From(Failure)));
                UpdateDiagnosticSnapshot();
                return evidence;
            }
            finally
            {
                executingOperations.Clear();
                busy = false;
            }
        }

        private List<ArenaOperationResult> CompleteBatch(IReadOnlyList<ArenaQueuedOperation> batch, StateSnapshotReference barrier)
        {
            List<ArenaOperationResult> completed = new List<ArenaOperationResult>();
            string barrierText = barrier.CaptureId == 0 ? null : barrier.ChannelId.ToString("N") + ":" + barrier.CaptureId;
            foreach (ArenaQueuedOperation operation in batch)
            {
                ArenaOperationResult outcome = FindOutcome(operation.Handle.Sequence);
                if (outcome == null) outcome = new ArenaOperationResult(operation.Handle.Sequence, operation.TargetTick, OperationState.Failed, operation.Handle.Sequence == executingSequence ? "simulation.exception" : "tick.aborted", null);
                ArenaOperationResult result = controls.Complete(operation, outcome, barrierText);
                completed.Add(result);
                RecordTrace(new ArenaTraceEntry(Id, operation.TargetTick, operation.Handle.Sequence, "Operation", operation.Metadata.Type, outcome.Code, actor: operation.Metadata.Actor, target: operation.Metadata.Target));
            }
            return completed;
        }

        private ArenaQueuedOperation FindCurrentOperation(long sequence)
        {
            ArenaQueuedOperation operation;
            if (executingOperations.TryGetValue(sequence, out operation)) return operation;
            throw new InvalidOperationException("The executing Arena operation was not found.");
        }

        private ArenaOperationResult FindOutcome(long sequence)
        {
            foreach (Completion completion in completions)
            {
                if (completion.Operation.Handle.Sequence == sequence) return completion.Outcome;
            }
            return null;
        }

        private void StateSnapshotCaptureFailure(ulong tick, string code, Exception exception, string detail)
        {
            if (Failure != null) return;
            string exceptionType = exception == null ? null : exception.GetType().FullName;
            Failure = new ArenaFailure(tick, LastCompletedTick, executingSequence, stage, code, exceptionType, detail);
            State = ArenaSessionState.Faulted;
            controls.CancelPending("session.faulted");
            core.Stop();
            Diagnostic diagnostic = Diagnostic.Error("arena.session", code, detail ?? string.Empty);
            DiagnosticReport report = new DiagnosticReport(diagnostic, Id + ":" + tick + ":" + code, scopeId: Id, generation: epoch);
            diagnosticSink.Report(report);
            RecordTrace(new ArenaTraceEntry(Id, tick, executingSequence, stage, "Failure", code));
        }

        private void RecordOracleResults(ulong tick, EvaluationReport report)
        {
            foreach (OracleResult result in report.Results)
            {
                if (result.Verdict == TestVerdict.Passed) continue;
                RecordTrace(new ArenaTraceEntry(Id, tick, 0, "Oracle", result.Code, result.Detail));
                Diagnostic diagnostic = Diagnostic.Error("arena.oracle", result.Code, result.Detail);
                DiagnosticReport diagnosticReport = new DiagnosticReport(diagnostic, Id + ":" + tick + ":" + result.Code, scopeId: Id, generation: epoch);
                diagnosticSink.Report(diagnosticReport);
            }
        }

        private static OracleResult FindFailedOracleResult(EvaluationReport report)
        {
            foreach (OracleResult result in report.Results)
            {
                if (result.Verdict != TestVerdict.Passed && result.Verdict != TestVerdict.Skipped) return result;
            }
            return null;
        }

        private void RecordPhase(SimulationPhase phase, bool entering)
        {
            stage = phase.ToString();
            executingSequence = 0;
            RecordTrace(new ArenaTraceEntry(Id, core == null ? 0 : core.TickNumber, 0, "Phase", stage, entering ? "begin" : "end"));
        }

        private void RecordDispatch(MessageDispatch dispatch)
        {
            ArenaTraceMetadata metadata = definition.DescribeMessage(dispatch.Message);
            if (metadata == null)
            {
                string type = dispatch.Message.GetType().Name;
                RecordTrace(new ArenaTraceEntry(Id, core.TickNumber, 0, dispatch.Category.ToString(), type, string.Empty, dispatch.Wave));
                return;
            }
            executingSequence = metadata.Sequence;
            RecordTrace(new ArenaTraceEntry(Id, core.TickNumber, metadata.Sequence, dispatch.Category.ToString(), metadata.Type, metadata.Detail, dispatch.Wave, metadata.Actor, metadata.Target));
        }

        private void RecordTrace(ArenaTraceEntry entry)
        {
            trace.Writer.Record(entry);
        }

        private void UpdateDiagnosticSnapshot()
        {
            string faultCode = Failure == null ? null : Failure.Code;
            ulong observationTick = observation == null ? 0 : observation.Tick;
            latestDiagnostics = new ArenaDiagnosticSnapshot(Id, State, CurrentTick, observationTick, observationReference, observation, evaluation, faultCode, diagnosticSink.Reports);
        }

        private void EnsureIdle()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Use the Arena session owner thread.");
            if (disposed) throw new ObjectDisposedException(nameof(ArenaSession));
            if (busy) throw new InvalidOperationException("Arena session callbacks cannot reenter the host.");
        }
    }
}
