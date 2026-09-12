using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Arena.Integration;

namespace Arena.Composition
{
    [DataContract]
    public sealed class ArenaLimits
    {
        public ArenaLimits(int maxTicks = 10000, int maxInputs = 10000, int traceCapacity = 512, int maxPayloadBytes = 65536, int maxTotalPayloadBytes = 4194304)
        {
            MaxTicks = maxTicks;
            MaxInputs = maxInputs;
            TraceCapacity = traceCapacity;
            MaxPayloadBytes = maxPayloadBytes;
            MaxTotalPayloadBytes = maxTotalPayloadBytes;
            Validate();
        }

        [DataMember(Order = 1)] public int MaxTicks { get; private set; }
        [DataMember(Order = 2)] public int MaxInputs { get; private set; }
        [DataMember(Order = 3)] public int TraceCapacity { get; private set; }
        [DataMember(Order = 4)] public int MaxPayloadBytes { get; private set; }
        [DataMember(Order = 5)] public int MaxTotalPayloadBytes { get; private set; }

        public void Validate()
        {
            bool invalidCount = MaxTicks < 1 || MaxTicks > 100000 || MaxInputs < 1 || MaxInputs > 100000;
            bool invalidTrace = TraceCapacity < 1 || TraceCapacity > 65536;
            bool invalidPayload = MaxPayloadBytes < 1 || MaxPayloadBytes > 1048576 || MaxTotalPayloadBytes < MaxPayloadBytes || MaxTotalPayloadBytes > 16777216;
            if (invalidCount || invalidTrace || invalidPayload) throw new ArgumentException("Invalid Arena limits.");
        }

        public void CheckPayload(string payload)
        {
            if (payload == null || Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes) throw new ArgumentException("Null or oversized Arena payload.");
        }
    }

    [DataContract]
    public sealed class ArenaRecordedInput
    {
        public ArenaRecordedInput(long sequence, ulong tick, string payload)
        {
            Sequence = sequence;
            Tick = tick;
            Payload = payload;
        }

        [DataMember(Order = 1)] public long Sequence { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public string Payload { get; private set; }
    }

    [DataContract]
    public sealed class ArenaRecordedFailure
    {
        public ArenaRecordedFailure(ulong tick, ulong lastCompletedTick, long sequence, string stage, string code, string exceptionType, string detail)
        {
            Tick = tick;
            LastCompletedTick = lastCompletedTick;
            Sequence = sequence;
            Stage = stage;
            Code = code;
            ExceptionType = exceptionType;
            Detail = detail;
        }

        [DataMember(Order = 1)] public ulong Tick { get; private set; }
        [DataMember(Order = 2)] public ulong LastCompletedTick { get; private set; }
        [DataMember(Order = 3)] public long Sequence { get; private set; }
        [DataMember(Order = 4)] public string Stage { get; private set; }
        [DataMember(Order = 5)] public string Code { get; private set; }
        [DataMember(Order = 6)] public string ExceptionType { get; private set; }
        [DataMember(Order = 7)] public string Detail { get; private set; }

        public string Fingerprint
        {
            get { return Tick + ":" + LastCompletedTick + ":" + Sequence + ":" + Stage + ":" + Code + ":" + ExceptionType; }
        }

        public static ArenaRecordedFailure From(ArenaFailure failure)
        {
            if (failure == null) return null;
            return new ArenaRecordedFailure(failure.Tick, failure.LastCompletedTick, failure.Sequence, failure.Stage, failure.Code, failure.ExceptionType, failure.Detail);
        }
    }

    [DataContract]
    public sealed class ArenaRecordedTick
    {
        private ArenaOperationResult[] resultItems;

        public ArenaRecordedTick(ulong tick, string digest, IEnumerable<ArenaOperationResult> results, ArenaRecordedFailure failure)
        {
            Tick = tick;
            Digest = digest;
            resultItems = new List<ArenaOperationResult>(results).ToArray();
            Failure = failure;
        }

        [DataMember(Order = 1)] public ulong Tick { get; private set; }
        [DataMember(Order = 2)] public string Digest { get; private set; }
        [DataMember(Order = 3)] private ArenaOperationResult[] ResultItems
        {
            get { return resultItems; }
            set { resultItems = value; }
        }
        [DataMember(Order = 4)] public ArenaRecordedFailure Failure { get; private set; }
        public IReadOnlyList<ArenaOperationResult> Results
        {
            get { return Array.AsReadOnly(resultItems); }
        }

        public ArenaTickEvidence ToEvidence()
        {
            ArenaFailure failure = Failure == null ? null : new ArenaFailure(Failure.Tick, Failure.LastCompletedTick, Failure.Sequence, Failure.Stage, Failure.Code, Failure.ExceptionType, Failure.Detail);
            return new ArenaTickEvidence(0, Tick, Digest, Results, null, failure);
        }
    }

    [DataContract]
    public sealed class ArenaRecording
    {
        private ArenaRecordedInput[] inputItems;
        private ArenaRecordedTick[] tickItems;
        private ArenaRecordedTraceEntry[] traceItems;

        public ArenaRecording(string policy, string runtime, string scenario, float tickDelta, ArenaLimits limits, string initialDigest, IEnumerable<ArenaRecordedInput> inputs, IEnumerable<ArenaRecordedTick> ticks, IEnumerable<ArenaRecordedTraceEntry> trace, long droppedTraceEntries)
        {
            Schema = 2;
            Policy = policy;
            Runtime = runtime;
            Scenario = scenario;
            TickDelta = tickDelta;
            Limits = limits;
            InitialDigest = initialDigest;
            inputItems = new List<ArenaRecordedInput>(inputs).ToArray();
            tickItems = new List<ArenaRecordedTick>(ticks).ToArray();
            traceItems = new List<ArenaRecordedTraceEntry>(trace).ToArray();
            DroppedTraceEntries = droppedTraceEntries;
        }

        [DataMember(Order = 1)] public int Schema { get; private set; }
        [DataMember(Order = 2)] public string Policy { get; private set; }
        [DataMember(Order = 3)] public string Runtime { get; private set; }
        [DataMember(Order = 4)] public string Scenario { get; private set; }
        [DataMember(Order = 5)] public float TickDelta { get; private set; }
        [DataMember(Order = 6)] public ArenaLimits Limits { get; private set; }
        [DataMember(Order = 7)] public string InitialDigest { get; private set; }
        [DataMember(Order = 8)] private ArenaRecordedInput[] InputItems
        {
            get { return inputItems; }
            set { inputItems = value; }
        }
        [DataMember(Order = 9)] private ArenaRecordedTick[] TickItems
        {
            get { return tickItems; }
            set { tickItems = value; }
        }
        [DataMember(Order = 10)] private ArenaRecordedTraceEntry[] TraceItems
        {
            get { return traceItems; }
            set { traceItems = value; }
        }
        [DataMember(Order = 11)] public long DroppedTraceEntries { get; private set; }
        public IReadOnlyList<ArenaRecordedInput> Inputs
        {
            get { return Array.AsReadOnly(inputItems); }
        }
        public IReadOnlyList<ArenaRecordedTick> Ticks
        {
            get { return Array.AsReadOnly(tickItems); }
        }
        public IReadOnlyList<ArenaRecordedTraceEntry> Trace
        {
            get { return Array.AsReadOnly(traceItems); }
        }

        public void Validate()
        {
            bool invalidHeader = Schema != 2 || string.IsNullOrWhiteSpace(Policy) || string.IsNullOrWhiteSpace(Runtime) || string.IsNullOrWhiteSpace(InitialDigest);
            bool invalidTickDelta = float.IsNaN(TickDelta) || float.IsInfinity(TickDelta) || TickDelta <= 0;
            if (invalidHeader || invalidTickDelta || Limits == null || inputItems == null || tickItems == null || traceItems == null) throw new ArgumentException("Invalid Arena recording header.");
            Limits.Validate();
            Limits.CheckPayload(Scenario);
            if (inputItems.Length > Limits.MaxInputs || tickItems.Length > Limits.MaxTicks || traceItems.Length > Limits.TraceCapacity || DroppedTraceEntries < 0) throw new ArgumentException("Arena recording exceeds limits.");
            long expectedSequence = 1;
            long totalPayloadBytes = Encoding.UTF8.GetByteCount(Scenario);
            foreach (ArenaRecordedInput input in inputItems)
            {
                if (input == null || input.Sequence != expectedSequence || input.Tick == 0 || input.Tick > (ulong)Limits.MaxTicks) throw new ArgumentException("Invalid Arena recorded input.");
                Limits.CheckPayload(input.Payload);
                totalPayloadBytes += Encoding.UTF8.GetByteCount(input.Payload);
                expectedSequence++;
            }
            if (totalPayloadBytes > Limits.MaxTotalPayloadBytes) throw new ArgumentException("Arena recording payload budget exceeded.");
            for (int index = 0; index < tickItems.Length; index++)
            {
                ArenaRecordedTick tick = tickItems[index];
                if (tick == null || tick.Tick != (ulong)index + 1) throw new ArgumentException("Invalid Arena tick sequence.");
                if (string.IsNullOrWhiteSpace(tick.Digest) && tick.Failure == null) throw new ArgumentException("A successful Arena tick requires a state digest.");
            }
        }
    }

    public static class ArenaRecordingIO
    {
        public static ArenaRecording Read(Stream source, int maxBytes = 16777216)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maxBytes < 1 || maxBytes > 67108864) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            using (MemoryStream bounded = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bounded.Length + read > maxBytes) throw new ArgumentException("Arena recording exceeds byte limit.");
                    bounded.Write(buffer, 0, read);
                }
                bounded.Position = 0;
                ArenaRecording recording = ArenaCodecs.Read<ArenaRecording>(bounded);
                if (recording == null) throw new ArgumentException("Arena recording cannot be null.");
                recording.Validate();
                return recording;
            }
        }

        public static void Write(Stream destination, ArenaRecording recording)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (recording == null) throw new ArgumentNullException(nameof(recording));
            recording.Validate();
            ArenaCodecs.Write(destination, recording);
        }
    }
}
