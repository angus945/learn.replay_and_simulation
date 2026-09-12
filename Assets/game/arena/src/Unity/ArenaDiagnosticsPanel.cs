using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Arena.Integration;
using Module.Verification.Oracle;
using Module.Verification.TraceBuffer;
using Module.Verification.SystemFact.Observability;

namespace Arena.Unity
{
    /// <summary>A cached view row. Formatting happens when a retained record arrives, never while drawing.</summary>
    public sealed class ArenaTraceRow
    {
        private const int SummaryLineLength = 64;

        internal ArenaTraceRow(ObservedFact observedFact)
        {
            IArenaTraceFact entry = observedFact.Fact as IArenaTraceFact;
            if (entry == null) throw new ArgumentException("The observed fact is not part of the Arena trace language.", nameof(observedFact));
            Sequence = observedFact.Context.Sequence;
            string first = string.Format(CultureInfo.InvariantCulture, "#{0}  t{1}  {2}/{3}",
                Sequence, entry.Tick, entry.Stage, entry.Type);
            string second = string.Format(CultureInfo.InvariantCulture, "w{0}  {1} > {2}  {3}",
                entry.Wave, entry.Actor, entry.Target, entry.Code);
            Summary = LimitLine(first) + "\n" + LimitLine(second);
            Detail = string.Format(CultureInfo.InvariantCulture,
                "Record #{0} / input sequence {1}\nSession {2}\nTick {3} / wave {4}\n{5}/{6}\nActor {7} > target {8}\n{9}",
                Sequence, entry.OperationSequence, entry.SessionId, entry.Tick, entry.Wave,
                entry.Stage, entry.Type, entry.Actor, entry.Target, entry.Code);
        }

        public long Sequence { get; }
        public string Summary { get; }
        public string Detail { get; }

        private static string LimitLine(string text)
        {
            string line = text.Replace('\r', ' ').Replace('\n', ' ');
            return line.Length <= SummaryLineLength ? line : line.Substring(0, SummaryLineLength - 3) + "...";
        }
    }

    /// <summary>
    /// Read-only diagnostics presenter. It cannot Submit, Step, Reset or access Admin.
    /// The retained-mode view consumes stable cached text and a bounded, newest-first row collection.
    /// </summary>
    public sealed class ArenaDiagnosticsPanel
    {
        private const int HistoryCapacity = 160;
        private const int BatchCapacity = 512;
        private readonly IArenaDiagnosticReader reader;
        private readonly List<ArenaTraceRow> history = new List<ArenaTraceRow>(HistoryCapacity);
        private TraceCursor cursor;
        private float nextPoll;
        private bool visible = true;
        private bool refreshImmediately = true;

        public ArenaDiagnosticsPanel(IArenaDiagnosticReader reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            // IList is the UI Toolkit ListView contract. AsReadOnly prevents a view from changing history.
            TraceRows = history.AsReadOnly();
        }

        public ArenaDiagnosticSnapshot Snapshot { get; private set; }
        public long MissedCount { get; private set; }
        public long SourceOverwrittenCount { get; private set; }
        public long LocalEvictedCount { get; private set; }
        public bool HasMore { get; private set; }
        public int HistoryCount
        {
            get { return history.Count; }
        }
        public IList TraceRows { get; }
        public int TraceRevision { get; private set; }
        public int Revision { get; private set; }
        public long FormattedTraceCount { get; private set; }
        public string SessionText { get; private set; } = string.Empty;
        public string StateText { get; private set; } = "Waiting for the first diagnostic snapshot.";
        public string InvariantText { get; private set; } = string.Empty;
        public string ObservationText { get; private set; } = string.Empty;
        public string TraceStatusText { get; private set; } = string.Empty;
        public string ErrorText { get; private set; } = string.Empty;

        /// <summary>Hidden panels do no scheduled reads or formatting; reopening refreshes immediately.</summary>
        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (visible) refreshImmediately = true;
        }

        /// <summary>Explicit polling bypasses visibility and throttling, including in deterministic tests.</summary>
        public void Poll()
        {
            ArenaDiagnosticSnapshot next = reader.ReadSnapshot();
            bool sessionChanged = Snapshot != null && !string.Equals(Snapshot.SessionId, next.SessionId, StringComparison.Ordinal);
            TraceRead<ObservedFact> batch = reader.ReadTrace(sessionChanged ? default : cursor, BatchCapacity);
            bool reset = sessionChanged || batch.State == TraceReadState.ForeignCursor;
            long previousMissed = MissedCount;
            long previousOverwritten = SourceOverwrittenCount;
            long previousEvicted = LocalEvictedCount;
            bool previousHasMore = HasMore;

            if (reset)
            {
                history.Clear();
                MissedCount = 0;
                SourceOverwrittenCount = 0;
                LocalEvictedCount = 0;
            }
            MissedCount += batch.MissedCount;
            SourceOverwrittenCount = batch.OverwrittenCount;
            HasMore = batch.HasMore;

            int received = batch.Items.Count;
            int evicted = Math.Max(0, history.Count + received - HistoryCapacity);
            LocalEvictedCount += evicted;
            int retainedFromBatch = Math.Min(HistoryCapacity, received);
            int retainedFromHistory = Math.Min(history.Count, HistoryCapacity - retainedFromBatch);
            if (history.Count > retainedFromHistory)
                history.RemoveRange(retainedFromHistory, history.Count - retainedFromHistory);
            // Do not format records that would be immediately trimmed out of a large incoming batch.
            for (int index = received - retainedFromBatch; index < received; index++)
            {
                history.Insert(0, new ArenaTraceRow(batch.Items[index]));
                FormattedTraceCount++;
            }
            cursor = batch.NextCursor;
            if (reset || received != 0) TraceRevision++;

            bool textChanged = UpdateSnapshotText(next);
            if (Snapshot == null || previousMissed != MissedCount || previousOverwritten != SourceOverwrittenCount ||
                previousEvicted != LocalEvictedCount || previousHasMore != HasMore)
            {
                TraceStatusText = string.Format(CultureInfo.InvariantCulture,
                    "Source overwritten {0} · missed {1}\nPanel trimmed {2} · {3}",
                    SourceOverwrittenCount, MissedCount, LocalEvictedCount,
                    HasMore ? "reading backlog" : "cursor up to date");
                textChanged = true;
            }
            if (ErrorText.Length != 0)
            {
                ErrorText = string.Empty;
                textChanged = true;
            }
            Snapshot = next;
            if (textChanged) Revision++;
        }

        public void Refresh(float realtime)
        {
            if (!visible || (!refreshImmediately && realtime < nextPoll)) return;
            refreshImmediately = false;
            nextPoll = realtime + .1f;
            try { Poll(); }
            catch (Exception exception)
            {
                string nextError = exception.GetType().Name + ": " + exception.Message;
                if (string.Equals(ErrorText, nextError, StringComparison.Ordinal)) return;
                ErrorText = nextError;
                Revision++;
            }
        }

        private bool UpdateSnapshotText(ArenaDiagnosticSnapshot next)
        {
            ArenaDiagnosticSnapshot previous = Snapshot;
            bool changed = false;
            if (previous == null || !string.Equals(previous.SessionId, next.SessionId, StringComparison.Ordinal))
            {
                string session = next.SessionId ?? string.Empty;
                SessionText = "SESSION  " + (session.Length > 12 ? session.Substring(0, 12) : session);
                changed = true;
            }
            if (previous == null || previous.State != next.State || previous.Tick != next.Tick ||
                previous.ObservationTick != next.ObservationTick || !string.Equals(previous.FaultCode, next.FaultCode, StringComparison.Ordinal))
            {
                StateText = string.Format(CultureInfo.InvariantCulture, "STATE  {0} / TICK {1}", next.State, next.Tick);
                if (next.ObservationTick != next.Tick)
                    StateText += string.Format(CultureInfo.InvariantCulture,
                        "\nLast complete observation: t{0} (stale after failure)", next.ObservationTick);
                if (!string.IsNullOrEmpty(next.FaultCode)) StateText += "\nFAULT  " + next.FaultCode;
                changed = true;
            }
            if (previous == null || !SameInvariantText(previous.Evaluation, next.Evaluation))
            {
                InvariantText = FormatInvariants(next.Evaluation);
                changed = true;
            }
            if (previous == null || !SameObservationText(previous.Observation, next.Observation))
            {
                ObservationText = FormatObservation(next.Observation);
                changed = true;
            }
            return changed;
        }

        private static bool SameInvariantText(EvaluationReport left, EvaluationReport right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Verdict != right.Verdict || left.Results.Count != right.Results.Count || left.Errors.Count != right.Errors.Count) return false;
            for (int index = 0; index < left.Results.Count; index++)
            {
                OracleResult first = left.Results[index];
                OracleResult second = right.Results[index];
                if (!string.Equals(first.Code, second.Code, StringComparison.Ordinal) ||
                    !string.Equals(first.Detail, second.Detail, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool SameObservationText(ArenaObservation left, ArenaObservation right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Actors.Count != right.Actors.Count ||
                left.EnemiesSpawned != right.EnemiesSpawned || left.PendingRespawnTicks.Count != right.PendingRespawnTicks.Count) return false;
            for (int index = 0; index < left.Actors.Count; index++)
            {
                ActorSnapshot first = left.Actors[index];
                ActorSnapshot second = right.Actors[index];
                if (first.Id != second.Id || first.Enemy != second.Enemy || first.Health != second.Health ||
                    first.MaxHealth != second.MaxHealth || first.X != second.X || first.Y != second.Y) return false;
            }
            return true;
        }

        private static string FormatInvariants(EvaluationReport report)
        {
            if (report == null) return "ORACLES  NOT AVAILABLE";
            string checks = report.Verdict.ToString().ToUpperInvariant();
            StringBuilder text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "ORACLES  {0} / {1} checks\nReads do not run evaluation", checks, report.Results.Count);
            foreach (OracleResult result in report.Results)
            {
                if (result.Verdict == TestVerdict.Passed) continue;
                text.Append('\n').Append(result.Code).Append(": ").Append(result.Detail);
            }
            return text.ToString();
        }

        private static string FormatObservation(ArenaObservation observation)
        {
            if (observation == null) return "No complete observation.";
            StringBuilder text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "{0} actors / {1} enemies spawned\nPending respawns: {2}",
                observation.Actors.Count, observation.EnemiesSpawned, observation.PendingRespawnTicks.Count);
            foreach (ActorSnapshot actor in observation.Actors)
                text.AppendFormat(CultureInfo.InvariantCulture, "\n#{0} {1}  HP {2}/{3}  ({4:F2}, {5:F2})",
                    actor.Id, actor.Enemy ? "ENEMY" : "PLAYER", actor.Health, actor.MaxHealth, actor.X, actor.Y);
            return text.ToString();
        }
    }
}
