using System;
using System.Collections;
using Arena.Composition;
using Arena.Integration;
using Arena.Unity;
using InvariantChecks;
using NUnit.Framework;
using Testability;
using Testability.Templates;
using TraceBuffering;

namespace Arena.Tests.PlayMode
{
    public sealed class ArenaDiagnosticsPresenterTests
    {
        [Test]
        public void StablePollReusesRowsAndTextDespiteFreshSnapshotObjects()
        {
            CountingReader reader = new CountingReader(32);
            reader.Append(3);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Poll();
            IList rows = panel.TraceRows;
            ArenaTraceRow newest = (ArenaTraceRow)rows[0];
            string session = panel.SessionText;
            string state = panel.StateText;
            string invariants = panel.InvariantText;
            string observation = panel.ObservationText;
            string traceStatus = panel.TraceStatusText;
            DiagnosticSnapshot<ArenaObservation> initial = panel.Snapshot;
            int revision = panel.Revision;
            int traceRevision = panel.TraceRevision;
            for (int index = 0; index < 5; index++) panel.Poll();
            Assert.That(panel.Snapshot, Is.Not.SameAs(initial));
            Assert.That(panel.TraceRows, Is.SameAs(rows));
            Assert.That(panel.TraceRows[0], Is.SameAs(newest));
            Assert.That(panel.FormattedTraceCount, Is.EqualTo(3));
            Assert.That(panel.Revision, Is.EqualTo(revision));
            Assert.That(panel.TraceRevision, Is.EqualTo(traceRevision));
            Assert.That(panel.SessionText, Is.SameAs(session));
            Assert.That(panel.StateText, Is.SameAs(state));
            Assert.That(panel.InvariantText, Is.SameAs(invariants));
            Assert.That(panel.ObservationText, Is.SameAs(observation));
            Assert.That(panel.TraceStatusText, Is.SameAs(traceStatus));
            Assert.That(rows.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => rows.Add(null));
        }

        [Test]
        public void HiddenPanelStopsReadingAndReopensWithoutWaitingForThrottle()
        {
            CountingReader reader = new CountingReader(32);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Refresh(0);
            panel.SetVisible(false);
            reader.Append(2);
            panel.Refresh(.02f);
            panel.Refresh(10);
            Assert.That(reader.SnapshotReads, Is.EqualTo(1));
            Assert.That(reader.TraceReads, Is.EqualTo(1));
            Assert.That(panel.FormattedTraceCount, Is.Zero);
            panel.SetVisible(true);
            panel.Refresh(.03f); // Reopening does not wait until the old .1-second deadline.
            Assert.That(reader.TraceReads, Is.EqualTo(2));
            Assert.That(panel.FormattedTraceCount, Is.EqualTo(2));
            panel.Refresh(.04f);
            Assert.That(reader.TraceReads, Is.EqualTo(2));
            panel.Refresh(.14f);
            Assert.That(reader.TraceReads, Is.EqualTo(3));
            panel.SetVisible(false);
            panel.Poll(); // Explicit test/debug polling remains available while hidden.
            Assert.That(reader.TraceReads, Is.EqualTo(4));
        }

        [Test]
        public void LargeBatchFormatsOnlyRetainedRowsAndKeepsNewestFirst()
        {
            CountingReader reader = new CountingReader(1024);
            reader.Append(400);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Poll();
            Assert.That(reader.LastMaximum, Is.EqualTo(512));
            Assert.That(panel.HistoryCount, Is.EqualTo(160));
            Assert.That(panel.FormattedTraceCount, Is.EqualTo(160), "Discarded rows must never be formatted.");
            Assert.That(panel.LocalEvictedCount, Is.EqualTo(240));
            Assert.That(panel.MissedCount, Is.Zero, "Local UI trimming is not a source cursor gap.");
            Assert.That(panel.SourceOverwrittenCount, Is.Zero);
            Assert.That(((ArenaTraceRow)panel.TraceRows[0]).Sequence, Is.EqualTo(400));
            Assert.That(((ArenaTraceRow)panel.TraceRows[159]).Sequence, Is.EqualTo(241));
            ArenaTraceRow retained = (ArenaTraceRow)panel.TraceRows[0];
            reader.Append(1);
            panel.Poll();
            Assert.That(panel.FormattedTraceCount, Is.EqualTo(161));
            Assert.That(panel.TraceRows[1], Is.SameAs(retained));
            Assert.That(((ArenaTraceRow)panel.TraceRows[0]).Sequence, Is.EqualTo(401));
            Assert.That(panel.LocalEvictedCount, Is.EqualTo(241));
        }

        [Test]
        public void StreamResetClearsRowsAndRestartsGapCountersWithoutReplacingList()
        {
            CountingReader reader = new CountingReader(8);
            reader.Append(12);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Poll();
            Assert.That(panel.MissedCount, Is.EqualTo(4));
            Assert.That(panel.SourceOverwrittenCount, Is.EqualTo(4));
            IList rows = panel.TraceRows;
            int revision = panel.TraceRevision;
            reader.ResetStream(); // The same session can expose a replacement trace stream.
            reader.Append(1, "new-stream");
            panel.Poll();
            Assert.That(panel.TraceRows, Is.SameAs(rows));
            Assert.That(panel.HistoryCount, Is.EqualTo(1));
            Assert.That(panel.TraceRevision, Is.EqualTo(revision + 1));
            Assert.That(((ArenaTraceRow)rows[0]).Sequence, Is.EqualTo(1));
            Assert.That(((ArenaTraceRow)rows[0]).Detail, Does.Contain("new-stream"));
            Assert.That(panel.MissedCount, Is.Zero);
            Assert.That(panel.SourceOverwrittenCount, Is.Zero);
            Assert.That(panel.LocalEvictedCount, Is.Zero);
            revision = panel.TraceRevision;
            reader.SessionId = "second-session";
            reader.ResetStream();
            panel.Poll();
            Assert.That(panel.HistoryCount, Is.Zero);
            Assert.That(panel.TraceRevision, Is.EqualTo(revision + 1), "An empty new stream still invalidates the view.");
            Assert.That(panel.SessionText, Does.Contain("second-sessi"));
        }

        [Test]
        public void SummaryHasTwoBoundedLinesWhileTooltipRetainsFullEvidence()
        {
            CountingReader reader = new CountingReader(8);
            string detail = new string('x', 300) + "\nfull-detail-tail";
            reader.Append(1, detail);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Poll();
            ArenaTraceRow row = (ArenaTraceRow)panel.TraceRows[0];
            string[] lines = row.Summary.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0].Length, Is.LessThanOrEqualTo(64));
            Assert.That(lines[1].Length, Is.LessThanOrEqualTo(64));
            Assert.That(row.Detail, Does.Contain(detail));
            Assert.That(row.Detail, Does.Contain("input sequence"));
        }

        [Test]
        public void BacklogAndSourceGapsRemainDistinctFromLocalTrimming()
        {
            CountingReader reader = new CountingReader(800);
            reader.Append(900);
            ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(reader);
            panel.Poll();
            Assert.That(panel.HasMore, Is.True);
            Assert.That(panel.MissedCount, Is.EqualTo(100));
            Assert.That(panel.SourceOverwrittenCount, Is.EqualTo(100));
            Assert.That(panel.LocalEvictedCount, Is.EqualTo(352));
            Assert.That(panel.FormattedTraceCount, Is.EqualTo(160));
            panel.Poll();
            Assert.That(panel.HasMore, Is.False);
            Assert.That(panel.MissedCount, Is.EqualTo(100), "A cursor gap is counted once, not once per poll.");
            Assert.That(panel.SourceOverwrittenCount, Is.EqualTo(100));
            Assert.That(panel.LocalEvictedCount, Is.EqualTo(640));
            Assert.That(((ArenaTraceRow)panel.TraceRows[0]).Sequence, Is.EqualTo(900));
        }

        [Test]
        public void PresentationPollingCannotDriveSimulationOrChangeRecordingEvidence()
        {
            using (ArenaLiveSession session = new ArenaLiveSession(new ArenaScenario(tickDelta: .125f)))
            {
                session.CaptureAxes(1, 0);
                session.AdvanceTime(.25f);
                TemplateRecording before = session.CaptureRecording();
                ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(session.Diagnostics);
                for (int index = 0; index < 10; index++) panel.Poll();
                TemplateRecording after = session.CaptureRecording();
                Assert.That(session.TickNumber, Is.EqualTo(2));
                Assert.That(after.Ticks.Count, Is.EqualTo(before.Ticks.Count));
                Assert.That(after.Ticks[1].Hash, Is.EqualTo(before.Ticks[1].Hash));
                Assert.That(after.Inputs.Count, Is.EqualTo(before.Inputs.Count));
                Assert.That(panel.Snapshot.Tick, Is.EqualTo(2));
            }
        }

        private sealed class CountingReader : IDiagnosticReader<ArenaObservation>
        {
            private readonly int capacity;
            private TraceBuffer<TraceEntry> trace;

            public CountingReader(int capacity)
            {
                this.capacity = capacity;
                trace = new TraceBuffer<TraceEntry>(capacity);
            }

            public string SessionId { get; set; } = "presenter-test-session";
            public int SnapshotReads { get; private set; }
            public int TraceReads { get; private set; }
            public int LastMaximum { get; private set; }

            public void Append(int count, string code = "received")
            {
                for (int index = 0; index < count; index++)
                    trace.Writer.Record(new TraceEntry(SessionId, 0, (ulong)index, "Test", "Fact", code));
            }

            public void ResetStream() { trace = new TraceBuffer<TraceEntry>(capacity); }

            public DiagnosticSnapshot<ArenaObservation> ObserveDiagnostics()
            {
                SnapshotReads++;
                // Fresh equivalent report/snapshot objects must not force presenter text allocation.
                return new DiagnosticSnapshot<ArenaObservation>(SessionId, SessionState.Running, 0, null,
                    new InvariantReport(false, 0, 0, Array.Empty<InvariantViolation>()), null);
            }

            public TraceBatch<TraceEntry> ReadTrace(TraceCursor cursor, int maxItems)
            {
                TraceReads++;
                LastMaximum = maxItems;
                return trace.Reader.Read(cursor, maxItems);
            }
        }
    }
}
