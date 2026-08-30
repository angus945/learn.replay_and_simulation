using System;

namespace DiagnosticTrace
{
    /// <summary>Single-threaded bounded journal. Payload T must be immutable; snapshots do not deep-clone T.</summary>
    public sealed class TraceBuffer<T>
    {
        private readonly TraceRecord<T>[] records;
        private readonly Guid streamId = Guid.NewGuid();
        private int next;
        private int count;
        private long lastSequence;

        public TraceBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            records = new TraceRecord<T>[capacity];
            Reader = new ReaderPort(this);
            Writer = new WriterPort(this);
        }
        public ITraceReader<T> Reader { get; }
        public ITraceWriter<T> Writer { get; }
        public int Capacity => records.Length;
        public long OverwrittenCount => lastSequence - count;

        private void Record(T entry)
        {
            if (ReferenceEquals(entry, null)) throw new ArgumentNullException(nameof(entry));
            if (lastSequence == long.MaxValue) throw new InvalidOperationException("Trace sequence exhausted.");
            records[next] = new TraceRecord<T>(++lastSequence, entry);
            next = (next + 1) % records.Length;
            if (count < records.Length) count++;
        }

        private TraceBatch<T> Read(TraceCursor cursor, int maxItems)
        {
            if (maxItems < 1) throw new ArgumentOutOfRangeException(nameof(maxItems));
            bool changed = cursor.StreamId != Guid.Empty && cursor.StreamId != streamId;
            long after = changed ? 0 : cursor.AfterSequence;
            if (after > lastSequence) throw new ArgumentOutOfRangeException(nameof(cursor), "Cursor is ahead of this stream.");
            long overwritten = lastSequence - count;
            long missed = Math.Max(0, overwritten - after);
            long effectiveAfter = Math.Max(after, overwritten);
            int take = (int)Math.Min(maxItems, lastSequence - effectiveAfter);
            TraceRecord<T>[] copy = new TraceRecord<T>[take];
            int oldestIndex = (next - count + records.Length) % records.Length;
            for (int i = 0; i < take; i++)
            {
                int offset = (int)(effectiveAfter - overwritten) + i;
                copy[i] = records[(oldestIndex + offset) % records.Length];
            }
            long consumed = effectiveAfter + take;
            return new TraceBatch<T>(copy, new TraceCursor(streamId, consumed), missed, overwritten, changed, consumed < lastSequence);
        }

        private sealed class ReaderPort : ITraceReader<T>
        {
            private readonly TraceBuffer<T> owner;
            internal ReaderPort(TraceBuffer<T> owner) { this.owner = owner; }
            public TraceBatch<T> Read(TraceCursor cursor, int maxItems) => owner.Read(cursor, maxItems);
        }
        private sealed class WriterPort : ITraceWriter<T>
        {
            private readonly TraceBuffer<T> owner;
            internal WriterPort(TraceBuffer<T> owner) { this.owner = owner; }
            public void Record(T entry) => owner.Record(entry);
        }
    }
}
