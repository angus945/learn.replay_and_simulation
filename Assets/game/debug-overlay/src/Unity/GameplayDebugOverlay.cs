using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TraceBuffering;
using GameplaySimulation;
using InvariantChecks;
using Testability;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DebugOverlay.Unity
{
    /// <summary>Read-only project panel. Only a diagnostics facade is injected; no host/session/control reference.</summary>
    public sealed class GameplayDebugOverlay : MonoBehaviour
    {
        private ReadOnlyDiagnosticsModel<GameplayObservation> model;
        private bool visible = true;
        private bool toggleDown;
        private float nextPoll;
        private Vector2 scroll;
        private string status = "Waiting for diagnostics";
        private string actors = string.Empty;
        private string checks = string.Empty;
        private string traces = string.Empty;
        private int traceLines;
        private GUIStyle label;
        private GUIStyle heading;
        public bool IsVisible => visible;

        public void Bind(IDiagnosticReader<GameplayObservation> reader)
        {
            model = new ReadOnlyDiagnosticsModel<GameplayObservation>(reader);
            nextPoll = 0;
        }

        private void LateUpdate()
        {
            bool down = Application.isFocused && Keyboard.current != null && Keyboard.current.f3Key.isPressed;
            if (down && !toggleDown) visible = !visible;
            toggleDown = down;
            if (!visible || model == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .2f; // UI refresh only; never used as simulation time.
            try
            {
                model.Poll();
                RebuildText();
            }
            catch (Exception exception)
            {
                status = "Diagnostic read error: " + exception.GetType().Name;
                checks = exception.Message;
            }
        }

        private void RebuildText()
        {
            DiagnosticSnapshot<GameplayObservation> snapshot = model.Snapshot;
            string id = snapshot.SessionId.Length > 8 ? snapshot.SessionId.Substring(0, 8) : snapshot.SessionId;
            status = $"Session {id}  |  {snapshot.State}  |  tick {snapshot.Tick}";
            StringBuilder text = new StringBuilder();
            foreach (ActorObservation actor in snapshot.Observation.Actors)
                text.AppendFormat(CultureInfo.InvariantCulture,
                    "#{0}  {1}  HP {2}/{3}\n  pos ({4:F2}, {5:F2})  dir ({6:F2}, {7:F2})\n",
                    actor.Id, actor.Active ? "ACTIVE" : "DEAD", actor.Health, actor.MaxHealth, actor.X, actor.Y, actor.DirectionX, actor.DirectionY);
            actors = text.ToString();
            InvariantReport report = snapshot.Invariants;
            checks = !report.Evaluated ? "Invariants: NOT EVALUATED" :
                $"Invariants: {(report.Violations.Count == 0 ? "PASS" : "FAIL")} at tick {report.Tick}\nRegistered checks: {report.CheckCount}";
            if (report.Evaluated && report.Tick != snapshot.Tick) checks += " [STALE]";
            foreach (InvariantViolation violation in report.Violations) checks += "\n" + violation.Code + ": " + violation.Detail;
            if (!string.IsNullOrEmpty(snapshot.FaultCode)) checks += "\nFault: " + snapshot.FaultCode;
            IReadOnlyList<TraceRecord<TraceEntry>> history = model.History;
            text.Clear();
            // Newest first; own history is bounded independently of the source ring.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                TraceRecord<TraceEntry> record = history[i];
                TraceEntry entry = record.Entry;
                string code = entry.Type == "Gameplay" && entry.Stage == "StateHash" && entry.Code.Length > 12
                    ? entry.Code.Substring(0, 12) + "..." : entry.Code;
                text.AppendFormat(CultureInfo.InvariantCulture, "#{0} t{1} a{2} {3}/{4} w{5} [{6}->{7}] {8}\n",
                    record.Sequence, entry.Tick, entry.Sequence, entry.Stage, entry.Type, entry.Wave, entry.Actor, entry.Target, code);
            }
            traces = text.ToString(); traceLines = history.Count;
        }

        private void OnGUI()
        {
            if (!visible || model == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false, wordWrap = false };
                heading = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            }
            float width = Mathf.Min(520, Screen.width * .47f);
            float height = Mathf.Min(540, Screen.height - 24);
            Rect panel = new Rect(Screen.width - width - 12, 12, width, height);
            Color previousColor = GUI.color;
            GUI.color = new Color(.035f, .05f, .07f, .97f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 12, panel.y + 8, width - 24, height - 16));
            GUILayout.Label("DIAGNOSTICS / READ ONLY", heading);
            GUILayout.Label("F3: hide | WASD: move | Space: attack", label);
            GUILayout.Label(status, label);
            GUILayout.Space(4);
            GUILayout.Label(actors, label);
            GUILayout.Label(checks, label);
            GUILayout.Space(4);
            GUILayout.Label($"Trace | missed {model.MissedCount} | local trimmed {model.LocalEvictedCount}" , label);
            GUILayout.Label($"Source overwritten: {model.SourceOverwrittenCount}", label);
            GUILayout.Label(model.HasMore ? "Reading backlog..." : "Latest page consumed (newest first)", label);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            GUILayout.Label(traces, label, GUILayout.MinHeight(traceLines * 18), GUILayout.MinWidth(900));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
