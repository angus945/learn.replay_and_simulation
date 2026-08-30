using System;
using System.Collections.Generic;
using DiagnosticTrace;
using Testability;

namespace DebugOverlay
{
    /// <summary>Bounded incremental consumer; owns only view state, never gameplay state.</summary>
    public sealed class ReadOnlyDiagnosticsModel<TObservation>
    {
        private readonly IDiagnosticReader<TObservation> source;
        private readonly int capacity;
        private readonly int pageSize;
        private readonly Queue<TraceRecord<TraceEntry>> history = new Queue<TraceRecord<TraceEntry>>();
        private TraceCursor cursor;

        public ReadOnlyDiagnosticsModel(IDiagnosticReader<TObservation> source, int capacity = 200, int pageSize = 256)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (capacity < 1 || pageSize < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity; this.pageSize = pageSize;
        }
        public DiagnosticSnapshot<TObservation> Snapshot { get; private set; }
        public IReadOnlyList<TraceRecord<TraceEntry>> History => new List<TraceRecord<TraceEntry>>(history).AsReadOnly();
        public long MissedCount { get; private set; }
        public long LocalEvictedCount { get; private set; }
        public long SourceOverwrittenCount { get; private set; }
        public bool HasMore { get; private set; }
        public bool StreamChanged { get; private set; }

        public void Poll()
        {
            DiagnosticSnapshot<TObservation> next = source.ObserveDiagnostics();
            bool sessionChanged = Snapshot != null && Snapshot.SessionId != next.SessionId;
            Snapshot = next;
            if (sessionChanged) ClearHistory();
            if (string.IsNullOrEmpty(next.SessionId)) return;
            TraceBatch<TraceEntry> batch = source.ReadTrace(cursor, pageSize);
            if (batch.StreamChanged && !sessionChanged) ClearHistory();
            StreamChanged = batch.StreamChanged;
            MissedCount += batch.MissedCount;
            SourceOverwrittenCount = batch.OverwrittenCount;
            HasMore = batch.HasMore;
            foreach (TraceRecord<TraceEntry> record in batch.Items)
            {
                if (history.Count == capacity) { history.Dequeue(); LocalEvictedCount++; }
                history.Enqueue(record);
            }
            cursor = batch.NextCursor;
        }

        private void ClearHistory()
        {
            history.Clear(); MissedCount = 0; LocalEvictedCount = 0; SourceOverwrittenCount = 0; HasMore = false;
        }
    }
}
