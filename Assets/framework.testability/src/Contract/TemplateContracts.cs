using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Testability.Templates
{
    /// <summary>Project-authored diagnostic description. It never changes dispatch or replay input.</summary>
    public sealed class TemplateTraceMetadata
    {
        public TemplateTraceMetadata(string type, ulong sequence = 0, ulong actor = 0, ulong target = 0, string detail = "")
        {
            if (string.IsNullOrWhiteSpace(type) || type.Length > 256) throw new ArgumentException("A diagnostic type of at most 256 characters is required.", nameof(type));
            if (detail == null || detail.Length > 4096) throw new ArgumentException("Diagnostic detail must contain at most 4096 characters.", nameof(detail));
            Type = type; Sequence = sequence; Actor = actor; Target = target; Detail = detail;
        }
        public string Type { get; }
        public ulong Sequence { get; }
        public ulong Actor { get; }
        public ulong Target { get; }
        public string Detail { get; }
    }

    public sealed class InputOutcome
    {
        public InputOutcome(ActionStatus status, string code) { Status = status; Code = code; }
        public ActionStatus Status { get; }
        public string Code { get; }
    }

    [DataContract]
    public sealed class TemplateLimits
    {
        public TemplateLimits(int maxTicks = 10000, int maxInputs = 10000, int traceCapacity = 512, int maxPayloadBytes = 65536,
            int maxTotalPayloadBytes = 4194304)
        { MaxTicks = maxTicks; MaxInputs = maxInputs; TraceCapacity = traceCapacity; MaxPayloadBytes = maxPayloadBytes; MaxTotalPayloadBytes = maxTotalPayloadBytes; Validate(); }
        [DataMember(Order = 1)] public int MaxTicks { get; private set; }
        [DataMember(Order = 2)] public int MaxInputs { get; private set; }
        [DataMember(Order = 3)] public int TraceCapacity { get; private set; }
        [DataMember(Order = 4)] public int MaxPayloadBytes { get; private set; }
        [DataMember(Order = 5)] public int MaxTotalPayloadBytes { get; private set; }
        public void Validate()
        {
            if (MaxTicks < 1 || MaxTicks > 100000 || MaxInputs < 1 || MaxInputs > 100000 || TraceCapacity < 1 || TraceCapacity > 65536 ||
                MaxPayloadBytes < 1 || MaxPayloadBytes > 1048576 || MaxTotalPayloadBytes < MaxPayloadBytes || MaxTotalPayloadBytes > 16777216)
                throw new ArgumentException("Invalid template limits.");
        }
        internal void CheckPayload(string payload)
        {
            if (payload == null || Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
                throw new ArgumentException("Null or oversized payload.");
        }
    }

    [DataContract]
    public sealed class RecordedInput
    {
        public RecordedInput(ulong sequence, ulong tick, string payload) { Sequence = sequence; Tick = tick; Payload = payload; }
        [DataMember(Order = 1)] public ulong Sequence { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public string Payload { get; private set; }
    }

    [DataContract]
    public sealed class TemplateFailure
    {
        public TemplateFailure(ulong tick, ulong lastCompletedTick, ulong sequence, string stage, string code, string exceptionType, string detail)
        { Tick = tick; LastCompletedTick = lastCompletedTick; Sequence = sequence; Stage = stage; Code = code; ExceptionType = exceptionType; Detail = detail; }
        [DataMember(Order = 1)] public ulong Tick { get; private set; }
        [DataMember(Order = 2)] public ulong LastCompletedTick { get; private set; }
        [DataMember(Order = 3)] public ulong Sequence { get; private set; }
        [DataMember(Order = 4)] public string Stage { get; private set; }
        [DataMember(Order = 5)] public string Code { get; private set; }
        [DataMember(Order = 6)] public string ExceptionType { get; private set; }
        [DataMember(Order = 7)] public string Detail { get; private set; }
        internal string Fingerprint => Tick + ":" + LastCompletedTick + ":" + Sequence + ":" + Stage + ":" + Code + ":" + ExceptionType;
    }

    [DataContract]
    public sealed class TemplateTick
    {
        public TemplateTick(ulong tick, string hash, IEnumerable<ActionResult> results)
        { Tick = tick; Hash = hash; items = new List<ActionResult>(results).ToArray(); }
        [DataMember(Order = 1)] public ulong Tick { get; private set; }
        [DataMember(Order = 2)] public string Hash { get; private set; }
        [DataMember(Order = 3)] private ActionResult[] items;
        public IReadOnlyList<ActionResult> Results => Array.AsReadOnly(items);
        internal bool HasResults => items != null;
    }

    public sealed class TemplateActionLookup
    {
        internal TemplateActionLookup(string state, ActionResult result = null, string reason = null)
        { State = state; Result = result; CancellationReason = reason; }
        public string State { get; }
        public ActionResult Result { get; }
        public string CancellationReason { get; }
    }

    public sealed class TemplateActionResultPage
    {
        internal TemplateActionResultPage(IEnumerable<ActionResult> items, int nextIndex, bool hasMore)
        { Items = new List<ActionResult>(items).AsReadOnly(); NextIndex = nextIndex; HasMore = hasMore; }
        public IReadOnlyList<ActionResult> Items { get; }
        public int NextIndex { get; }
        public bool HasMore { get; }
    }

    [DataContract]
    public sealed class TemplateRecording
    {
        public TemplateRecording(string policy, string runtime, string scenario, float tickDelta, TemplateLimits limits,
            string initialHash, IEnumerable<RecordedInput> inputs, IEnumerable<TemplateTick> ticks, TemplateFailure failure,
            IEnumerable<TraceEntry> trace, long droppedTraceEntries)
        {
            Schema = 1; Policy = policy; Runtime = runtime; Scenario = scenario; TickDelta = tickDelta; Limits = limits;
            InitialHash = initialHash; inputItems = new List<RecordedInput>(inputs).ToArray(); tickItems = new List<TemplateTick>(ticks).ToArray();
            Failure = failure; traceItems = new List<TraceEntry>(trace).ToArray(); DroppedTraceEntries = droppedTraceEntries;
        }
        [DataMember(Order = 1)] public int Schema { get; private set; }
        [DataMember(Order = 2)] public string Policy { get; private set; }
        [DataMember(Order = 3)] public string Runtime { get; private set; }
        [DataMember(Order = 4)] public string Scenario { get; private set; }
        [DataMember(Order = 5)] public float TickDelta { get; private set; }
        [DataMember(Order = 6)] public TemplateLimits Limits { get; private set; }
        [DataMember(Order = 7)] public string InitialHash { get; private set; }
        [DataMember(Order = 8)] private RecordedInput[] inputItems;
        [DataMember(Order = 9)] private TemplateTick[] tickItems;
        [DataMember(Order = 10)] public TemplateFailure Failure { get; private set; }
        [DataMember(Order = 11)] private TraceEntry[] traceItems;
        [DataMember(Order = 12)] public long DroppedTraceEntries { get; private set; }
        public IReadOnlyList<RecordedInput> Inputs => Array.AsReadOnly(inputItems);
        public IReadOnlyList<TemplateTick> Ticks => Array.AsReadOnly(tickItems);
        public IReadOnlyList<TraceEntry> Trace => Array.AsReadOnly(traceItems);
        public void Validate()
        {
            if (Schema != 1 || string.IsNullOrWhiteSpace(Policy) || string.IsNullOrWhiteSpace(Runtime) || string.IsNullOrWhiteSpace(InitialHash) ||
                float.IsNaN(TickDelta) || float.IsInfinity(TickDelta) || TickDelta <= 0 || Limits == null || inputItems == null || tickItems == null || traceItems == null)
                throw new ArgumentException("Invalid recording header.");
            Limits.Validate(); Limits.CheckPayload(Scenario);
            if (inputItems.Length > Limits.MaxInputs || tickItems.Length > Limits.MaxTicks || traceItems.Length > Limits.TraceCapacity || DroppedTraceEntries < 0)
                throw new ArgumentException("Recording exceeds limits.");
            Dictionary<ulong, RecordedInput> admitted = new Dictionary<ulong, RecordedInput>();
            long totalPayloadBytes = Encoding.UTF8.GetByteCount(Scenario);
            foreach (RecordedInput input in inputItems)
            {
                if (input == null || input.Sequence == 0 || input.Tick == 0 || input.Tick > (ulong)Limits.MaxTicks || admitted.ContainsKey(input.Sequence))
                    throw new ArgumentException("Invalid recorded input.");
                Limits.CheckPayload(input.Payload); admitted.Add(input.Sequence, input);
                totalPayloadBytes += Encoding.UTF8.GetByteCount(input.Payload);
            }
            if (totalPayloadBytes > Limits.MaxTotalPayloadBytes) throw new ArgumentException("Recording payload budget exceeded.");
            HashSet<ulong> completed = new HashSet<ulong>();
            for (int i = 0; i < tickItems.Length; i++)
            {
                TemplateTick tick = tickItems[i];
                if (tick == null || tick.Tick != (ulong)i + 1 || !tick.HasResults) throw new ArgumentException("Invalid tick sequence.");
                if (string.IsNullOrWhiteSpace(tick.Hash) && !(Failure != null && i == tickItems.Length - 1)) throw new ArgumentException("Missing tick hash.");
                ulong previous = 0;
                foreach (ActionResult result in tick.Results)
                {
                    if (result == null || result.Sequence <= previous || !completed.Add(result.Sequence) || !admitted.TryGetValue(result.Sequence, out RecordedInput input) ||
                        input.Tick != tick.Tick || result.Tick != tick.Tick || !Enum.IsDefined(typeof(ActionStatus), result.Status) || string.IsNullOrWhiteSpace(result.Code))
                        throw new ArgumentException("Invalid recorded result.");
                    previous = result.Sequence;
                }
            }
            foreach (RecordedInput input in inputItems)
                if (input.Tick <= (ulong)tickItems.Length && !completed.Contains(input.Sequence)) throw new ArgumentException("Missing result.");
            if (Failure != null && (Failure.Tick == 0 || Failure.Tick != (ulong)tickItems.Length || Failure.LastCompletedTick != Failure.Tick - 1 ||
                string.IsNullOrWhiteSpace(Failure.Code) || string.IsNullOrWhiteSpace(Failure.Stage))) throw new ArgumentException("Invalid failure evidence.");
        }
    }
}
