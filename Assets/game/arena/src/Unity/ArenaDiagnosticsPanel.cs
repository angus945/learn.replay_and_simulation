using System;
using System.Collections.Generic;
using System.Globalization;
using Arena.Integration;
using InvariantChecks;
using Testability;
using TraceBuffering;
using UnityEngine;

namespace Arena.Unity
{
    /// <summary>This panel is given only the read-only capability: no session, Submit, Step, Reset or Admin.</summary>
    public sealed class ArenaDiagnosticsPanel
    {
        private const int HistoryCapacity = 160;
        private readonly IDiagnosticReader<ArenaObservation> reader;
        private readonly Queue<TraceRecord<TraceEntry>> history = new Queue<TraceRecord<TraceEntry>>();
        private TraceCursor cursor;
        private Vector2 scroll;
        private float nextPoll;
        private string error;
        private GUIStyle small;
        private GUIStyle heading;
        private GUIStyle trace;

        public ArenaDiagnosticsPanel(IDiagnosticReader<ArenaObservation> reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public DiagnosticSnapshot<ArenaObservation> Snapshot { get; private set; }
        public long MissedCount { get; private set; }
        public long SourceOverwrittenCount { get; private set; }
        public long LocalEvictedCount { get; private set; }
        public bool HasMore { get; private set; }
        public int HistoryCount => history.Count;

        public void Poll()
        {
            DiagnosticSnapshot<ArenaObservation> next = reader.ObserveDiagnostics();
            if (Snapshot != null && Snapshot.SessionId != next.SessionId)
            {
                history.Clear(); cursor = default;
                MissedCount = 0; SourceOverwrittenCount = 0; LocalEvictedCount = 0;
            }
            Snapshot = next;
            TraceBatch<TraceEntry> batch = reader.ReadTrace(cursor, 512);
            if (batch.StreamChanged) history.Clear();
            MissedCount += batch.MissedCount;
            SourceOverwrittenCount = batch.OverwrittenCount;
            HasMore = batch.HasMore;
            foreach (TraceRecord<TraceEntry> item in batch.Items)
            {
                if (history.Count == HistoryCapacity) { history.Dequeue(); LocalEvictedCount++; }
                history.Enqueue(item);
            }
            cursor = batch.NextCursor;
            error = null;
        }

        public void Refresh(float realtime)
        {
            if (realtime < nextPoll) return;
            nextPoll = realtime + .1f;
            try { Poll(); }
            catch (Exception exception) { error = exception.GetType().Name + ": " + exception.Message; }
        }

        public void Draw(Rect rect)
        {
            EnsureStyles();
            ArenaGui.Fill(rect, ArenaGui.Panel);
            ArenaGui.Fill(new Rect(rect.x, rect.y, 3, rect.height), ArenaGui.Cyan);
            GUILayout.BeginArea(new Rect(rect.x + 18, rect.y + 16, rect.width - 36, rect.height - 32));
            GUILayout.Label("EVIDENCE / READ ONLY", heading);
            GUILayout.Label("Observation + invariant report + trace cursor", small);
            GUILayout.Space(16);
            if (Snapshot == null)
            {
                GUILayout.Label(error ?? "Waiting for the first diagnostic snapshot.", small);
                GUILayout.EndArea();
                return;
            }
            string session = Snapshot.SessionId ?? string.Empty;
            if (session.Length > 12) session = session.Substring(0, 12);
            GUILayout.Label("SESSION  " + session, small);
            GUILayout.Label("STATE     " + Snapshot.State + "   /   TICK " + Snapshot.Tick, heading);
            if (Snapshot.ObservationTick != Snapshot.Tick)
                GUILayout.Label("Last complete observation: t" + Snapshot.ObservationTick + " (stale after failure)", small);
            InvariantReport report = Snapshot.Invariants;
            string checks = !report.Evaluated ? "NOT EVALUATED" : report.Violations.Count == 0 ? "PASS" : "FAIL";
            GUILayout.Space(10);
            GUILayout.Label("INVARIANTS  " + checks + "  /  " + report.CheckCount + " checks", heading);
            GUILayout.Label("Evaluated at tick " + report.Tick + " · reads do not run checks", small);
            foreach (InvariantViolation violation in report.Violations)
                GUILayout.Label(violation.Code + ": " + violation.Detail, small);
            if (!string.IsNullOrEmpty(Snapshot.FaultCode)) GUILayout.Label("FAULT  " + Snapshot.FaultCode, heading);
            GUILayout.Space(14);
            ArenaObservation observation = Snapshot.Observation;
            if (observation != null)
            {
                GUILayout.Label("COMMITTED WORLD", heading);
                GUILayout.Label(observation.Actors.Count + " actors  /  " + observation.EnemiesSpawned + " enemies spawned", small);
                GUILayout.Label("Pending respawns: " + observation.PendingRespawnTicks.Count, small);
                foreach (ActorSnapshot actor in observation.Actors)
                    GUILayout.Label(string.Format(CultureInfo.InvariantCulture,
                        "#{0} {1}  HP {2}/{3}  ({4:F2}, {5:F2})", actor.Id, actor.Enemy ? "ENEMY" : "PLAYER",
                        actor.Health, actor.MaxHealth, actor.X, actor.Y), small);
            }
            GUILayout.Space(16);
            GUILayout.Label("TRACE / NEWEST FIRST", heading);
            GUILayout.Label("Source overwritten " + SourceOverwrittenCount + " · missed " + MissedCount, small);
            GUILayout.Label("Panel trimmed " + LocalEvictedCount + (HasMore ? " · reading backlog" : " · cursor up to date"), small);
            GUILayout.Space(6);
            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            TraceRecord<TraceEntry>[] entries = history.ToArray();
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                TraceRecord<TraceEntry> record = entries[index];
                TraceEntry entry = record.Entry;
                string detail = entry.Code ?? string.Empty;
                if (detail.Length > 46) detail = detail.Substring(0, 43) + "...";
                string line = "#" + record.Sequence + "  t" + entry.Tick + "  " + entry.Stage + "/" + entry.Type +
                    "\n  w" + entry.Wave + "  " + entry.Actor + " > " + entry.Target + "  " + detail;
                GUILayout.Label(new GUIContent(line, entry.Code), trace);
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, small);
            GUILayout.Space(10);
            GUILayout.Label("NO SUBMIT · NO STEP · NO ADMIN", small);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (small != null) return;
            small = ArenaGui.Label(11, ArenaGui.Muted);
            heading = ArenaGui.Label(12, ArenaGui.Text, FontStyle.Bold);
            trace = ArenaGui.Label(10, new Color(.64f, .76f, .8f));
            trace.wordWrap = true;
        }
    }

    internal static class ArenaGui
    {
        internal static readonly Color Panel = new Color(.035f, .055f, .075f, .97f);
        internal static readonly Color Cyan = new Color(.18f, .9f, .93f);
        internal static readonly Color Coral = new Color(1f, .4f, .34f);
        internal static readonly Color Text = new Color(.9f, .95f, .97f);
        internal static readonly Color Muted = new Color(.46f, .61f, .68f);

        internal static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static GUIStyle Label(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = true };
            style.normal.textColor = color;
            return style;
        }
    }
}
