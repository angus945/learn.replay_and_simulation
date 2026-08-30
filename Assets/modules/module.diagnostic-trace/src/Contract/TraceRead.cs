using System;
using System.Collections.Generic;

namespace DiagnosticTrace
{
    /// <summary>Exclusive position. Default requests the oldest retained entry in a new stream.</summary>
    public readonly struct TraceCursor
    {
        public TraceCursor(Guid streamId, long afterSequence)
        {
            if (afterSequence < 0 || (streamId == Guid.Empty && afterSequence != 0))
                throw new ArgumentOutOfRangeException(nameof(afterSequence));
            StreamId = streamId; AfterSequence = afterSequence;
        }
        public Guid StreamId { get; }
        public long AfterSequence { get; }
    }

    public readonly struct TraceRecord<T>
    {
        internal TraceRecord(long sequence, T entry) { Sequence = sequence; Entry = entry; }
        public long Sequence { get; }
        public T Entry { get; }
    }

    public sealed class TraceBatch<T>
    {
        internal TraceBatch(TraceRecord<T>[] items, TraceCursor next, long missed, long overwritten,
            bool streamChanged, bool hasMore)
        {
            Items = Array.AsReadOnly(items); NextCursor = next; MissedCount = missed;
            OverwrittenCount = overwritten; StreamChanged = streamChanged; HasMore = hasMore;
        }
        public IReadOnlyList<TraceRecord<T>> Items { get; }
        public TraceCursor NextCursor { get; }
        public long MissedCount { get; }
        public long OverwrittenCount { get; }
        public bool StreamChanged { get; }
        public bool HasMore { get; }
    }
}
