using System;
using System.IO;
using GameplaySimulation;
using UnityEngine;

namespace MovementDemo.Unity
{
    public sealed partial class MovementDemoHost
    {
        private ReplayPlayback replay;
        private string replayPath = string.Empty;
        private string replayMessage = "Recording from tick 0. Save before leaving Play Mode.";
        private bool editingReplayPath;
        public ReplayPlaybackState? PlaybackState => replay?.State;
        public RerunDifference ReplayDifference => replay?.FirstDifference;

        public string SaveRecording()
        {
            if (replay != null) throw new InvalidOperationException("Return to live mode before recording.");
            string directory = Path.Combine(Application.persistentDataPath, "Replays");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "replay-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
            ReplayFile.SaveNew(path, session.CaptureReplay());
            replayPath = path;
            replayMessage = "Saved " + Path.GetFileName(path);
            Debug.Log("Replay saved: " + path);
            return path;
        }
        public void LoadReplay(string path)
        {
            ReplayPlayback next = new ReplayPlayback(ReplayFile.Load(path), currentBuild: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD"));
            session.CaptureAxes(0, 0); session.CaptureAttackButton(false);
            replay = next; replayPath = path;
            if (overlay != null) overlay.Bind(replay.Diagnostics);
            replayMessage = next.Warnings.Count == 0 ? "Loaded; paused at tick 0." : "Loaded: " + string.Join(", ", next.Warnings);
        }
        public void PlayReplay() => replay?.Play();
        public void PauseReplay() => replay?.Pause();
        public void StepReplay() { if (replay != null) replay.Step(); }
        public void RestartReplay()
        {
            if (replay == null) return;
            replay.Restart();
            if (overlay != null) overlay.Bind(replay.Diagnostics);
        }
        public void ReturnToLive()
        {
            if (replay == null) return;
            // Resume the original recording, not the replay's state. Playback never advances this session.
            replay = null;
            if (overlay != null) overlay.Bind(session.Diagnostics);
            replayMessage = "Resumed live recording (playback time excluded).";
        }
        private void DrawReplayControls()
        {
            float width = Mathf.Min(490, Screen.width * .5f - 24);
            Rect panel = new Rect(12, Mathf.Max(160, Screen.height - 160), width, 148);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + 6, width - 16, 136));
            string state = replay == null ? "LIVE / RECORDING" : "REPLAY / " + replay.State + " " + replay.Observe().Tick + "/" + replay.EndTick;
            GUILayout.Label(state);
            GUILayout.BeginHorizontal();
            GUI.enabled = replay == null;
            if (GUILayout.Button("Save recording")) TryReplayAction(() => SaveRecording());
            GUI.enabled = true;
            if (GUILayout.Button("Load path")) TryReplayAction(() => LoadReplay(replayPath));
            GUI.enabled = replay != null;
            if (GUILayout.Button("Return live")) ReturnToLive();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUI.SetNextControlName("ReplayFilePath");
            replayPath = GUILayout.TextField(replayPath);
            editingReplayPath = GUI.GetNameOfFocusedControl() == "ReplayFilePath";
            GUILayout.BeginHorizontal();
            GUI.enabled = replay != null && replay.State == ReplayPlaybackState.Paused;
            if (GUILayout.Button("Play")) PlayReplay();
            if (GUILayout.Button("Step")) TryReplayAction(StepReplay);
            GUI.enabled = replay != null && replay.State == ReplayPlaybackState.Playing;
            if (GUILayout.Button("Pause")) PauseReplay();
            GUI.enabled = replay != null;
            if (GUILayout.Button("Restart")) TryReplayAction(RestartReplay);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(replay?.FirstDifference == null ? replayMessage : "DIVERGED t" + replay.FirstDifference.Tick + " " + replay.FirstDifference.Category);
            GUILayout.EndArea();
        }
        private void TryReplayAction(Action action)
        {
            try { action(); }
            catch (Exception exception) { replayMessage = exception.GetType().Name + ": " + exception.Message; }
        }
    }
}
