using System;
using Module.Verification.RuntimeControl;
using Module.Verification.SystemFact;
using Module.Verification.SystemFact.Observability;
using Module.Verification.TraceBuffer;

namespace Arena.Integration
{
    public interface IArenaTraceFact : IExecutionFact
    {
        string SessionId { get; }
        ulong Tick { get; }
        long OperationSequence { get; }
        string Stage { get; }
        string Type { get; }
        string Code { get; }
        int Wave { get; }
        ulong Actor { get; }
        ulong Target { get; }
    }

    public sealed class ArenaTraceFact : IArenaTraceFact
    {
        public ArenaTraceFact(string sessionId, ulong tick, long operationSequence, string stage, string type, string code, int wave = -1, ulong actor = 0, ulong target = 0)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            Tick = tick;
            OperationSequence = operationSequence;
            Stage = stage ?? string.Empty;
            Type = type ?? string.Empty;
            Code = code ?? string.Empty;
            Wave = wave;
            Actor = actor;
            Target = target;
        }

        public string SessionId { get; }
        public ulong Tick { get; }
        public long OperationSequence { get; }
        public string Stage { get; }
        public string Type { get; }
        public string Code { get; }
        public int Wave { get; }
        public ulong Actor { get; }
        public ulong Target { get; }
    }

    public interface IArenaVerificationContext
    {
        string SessionId { get; }
        ulong Tick { get; }
    }

    public sealed class RuntimeControlFactAdapter<TResult> : IOperationTransitionObserver<TResult>
    {
        private readonly IArenaVerificationContext context;
        private readonly ISystemFactSink sink;

        public RuntimeControlFactAdapter(IArenaVerificationContext context, ISystemFactSink sink)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Observe(OperationTransition<TResult> transition)
        {
            string code = transition.Completion == null ? "operation." + transition.State.ToString().ToLowerInvariant() : transition.Completion.Code;
            ArenaTraceFact fact = new ArenaTraceFact(context.SessionId, context.Tick, transition.Handle.Sequence, "Operation", transition.Descriptor.OperationType, code);
            sink.Publish(fact);
        }
    }

    public sealed class SystemFactTraceObserver : ISystemFactObserver<ISystemFact>
    {
        private readonly ITraceWriter<ObservedFact> writer;

        public SystemFactTraceObserver(ITraceWriter<ObservedFact> writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Observe(ISystemFact fact, FactObservationContext context)
        {
            writer.Append(new ObservedFact(fact, context));
        }
    }
}
