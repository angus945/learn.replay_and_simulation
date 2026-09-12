using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Module.Verification.Diagnostics;
using Module.Verification.RuntimeControl;
using Module.Verification.StateSnapshot;
using Module.Verification.Oracle;
using Module.Verification.TraceBuffer;

namespace Arena.Integration
{
    public enum ArenaSessionState
    {
        Running,
        Stopped,
        Faulted
    }

    public sealed class ArenaInputExecutionContext
    {
        public ArenaInputExecutionContext(string sessionId, OperationHandle handle, ulong targetTick, DeterministicSimulation.IDomainEventSink events)
        {
            SessionId = sessionId;
            Handle = handle;
            TargetTick = targetTick;
            Events = events;
        }

        public string SessionId { get; }
        public OperationHandle Handle { get; }
        public ulong TargetTick { get; }
        public DeterministicSimulation.IDomainEventSink Events { get; }

        public ArenaInputExecutionContext WithEvents(DeterministicSimulation.IDomainEventSink events)
        {
            return new ArenaInputExecutionContext(SessionId, Handle, TargetTick, events);
        }
    }

    public sealed class ArenaTraceMetadata
    {
        public ArenaTraceMetadata(string type, long sequence = 0, ulong actor = 0, ulong target = 0, string detail = "")
        {
            if (string.IsNullOrWhiteSpace(type) || type.Length > 256) throw new ArgumentException("A trace type of at most 256 characters is required.", nameof(type));
            if (detail == null || detail.Length > 4096) throw new ArgumentException("Trace detail must contain at most 4096 characters.", nameof(detail));
            Type = type;
            Sequence = sequence;
            Actor = actor;
            Target = target;
            Detail = detail;
        }

        public string Type { get; }
        public long Sequence { get; }
        public ulong Actor { get; }
        public ulong Target { get; }
        public string Detail { get; }
    }

    [DataContract]
    public sealed class ArenaTraceEntry
    {
        public ArenaTraceEntry(string session, ulong tick, long sequence, string stage, string type, string code, int wave = -1, ulong actor = 0, ulong target = 0)
        {
            Session = session;
            Tick = tick;
            Sequence = sequence;
            Stage = stage;
            Type = type;
            Code = code;
            Wave = wave;
            Actor = actor;
            Target = target;
        }

        [DataMember(Order = 1)] public string Session { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public long Sequence { get; private set; }
        [DataMember(Order = 4)] public string Stage { get; private set; }
        [DataMember(Order = 5)] public string Type { get; private set; }
        [DataMember(Order = 6)] public string Code { get; private set; }
        [DataMember(Order = 7)] public int Wave { get; private set; }
        [DataMember(Order = 8)] public ulong Actor { get; private set; }
        [DataMember(Order = 9)] public ulong Target { get; private set; }
    }

    [DataContract]
    public sealed class ArenaOperationResult
    {
        public ArenaOperationResult(long sequence, ulong tick, OperationState state, string code, string observationBarrier)
        {
            Sequence = sequence;
            Tick = tick;
            State = state;
            Code = code;
            ObservationBarrier = observationBarrier;
        }

        [DataMember(Order = 1)] public long Sequence { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public OperationState State { get; private set; }
        [DataMember(Order = 4)] public string Code { get; private set; }
        [DataMember(Order = 5)] public string ObservationBarrier { get; private set; }
    }

    public sealed class ArenaOperationResultPage
    {
        public ArenaOperationResultPage(IEnumerable<ArenaOperationResult> items, int nextIndex, bool hasMore)
        {
            Items = new List<ArenaOperationResult>(items).AsReadOnly();
            NextIndex = nextIndex;
            HasMore = hasMore;
        }

        public IReadOnlyList<ArenaOperationResult> Items { get; }
        public int NextIndex { get; }
        public bool HasMore { get; }
    }

    public sealed class ArenaFailure
    {
        public ArenaFailure(ulong tick, ulong lastCompletedTick, long sequence, string stage, string code, string exceptionType, string detail)
        {
            Tick = tick;
            LastCompletedTick = lastCompletedTick;
            Sequence = sequence;
            Stage = stage;
            Code = code;
            ExceptionType = exceptionType;
            Detail = detail;
        }

        public ulong Tick { get; }
        public ulong LastCompletedTick { get; }
        public long Sequence { get; }
        public string Stage { get; }
        public string Code { get; }
        public string ExceptionType { get; }
        public string Detail { get; }
        public string Fingerprint
        {
            get { return Tick + ":" + LastCompletedTick + ":" + Sequence + ":" + Stage + ":" + Code + ":" + ExceptionType; }
        }
    }

    public sealed class ArenaTickEvidence
    {
        public ArenaTickEvidence(long scopeId, ulong tick, string digest, IEnumerable<ArenaOperationResult> results, EvaluationReport evaluation, ArenaFailure failure)
        {
            ScopeId = scopeId;
            Tick = tick;
            Digest = digest;
            Results = new List<ArenaOperationResult>(results).AsReadOnly();
            Evaluation = evaluation;
            Failure = failure;
        }

        public long ScopeId { get; }
        public ulong Tick { get; }
        public string Digest { get; }
        public IReadOnlyList<ArenaOperationResult> Results { get; }
        public EvaluationReport Evaluation { get; }
        public ArenaFailure Failure { get; }
    }

    public sealed class ArenaDiagnosticSnapshot
    {
        public ArenaDiagnosticSnapshot(string sessionId, ArenaSessionState state, ulong tick, ulong observationTick, StateSnapshotReference observationReference, ArenaObservation observation, EvaluationReport evaluation, string faultCode, IReadOnlyList<DiagnosticReport> diagnostics)
        {
            SessionId = sessionId;
            State = state;
            Tick = tick;
            ObservationTick = observationTick;
            StateSnapshotReference = observationReference;
            Observation = observation;
            Evaluation = evaluation;
            FaultCode = faultCode;
            Diagnostics = diagnostics;
        }

        public string SessionId { get; }
        public ArenaSessionState State { get; }
        public ulong Tick { get; }
        public ulong ObservationTick { get; }
        public StateSnapshotReference StateSnapshotReference { get; }
        public ArenaObservation Observation { get; }
        public EvaluationReport Evaluation { get; }
        public string FaultCode { get; }
        public IReadOnlyList<DiagnosticReport> Diagnostics { get; }
    }

    public interface IArenaDiagnosticReader
    {
        ArenaDiagnosticSnapshot ReadSnapshot();
        TraceBatch<ArenaTraceEntry> ReadTrace(TraceCursor cursor, int maxItems);
    }

    public interface IArenaOperationReader
    {
        OperationRead<ArenaOperationResult> Find(OperationHandle handle);
        ArenaOperationResultPage Read(int afterIndex, int maxItems);
    }
}
