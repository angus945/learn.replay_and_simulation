using System;
using System.IO;
using Arena.Composition;
using Arena.Integration;
using Testability.Templates;
using UnityEngine;

namespace Arena.Unity
{
    public sealed partial class ArenaHost
    {
        private TemplateReplay<ArenaRuntime, ArenaScenario, ArenaInput, ArenaObservation> replay;
        private string replayPath = string.Empty;
        private string replayMessage = "Recording every submitted input from tick 0. Save a run, then load it to verify replay.";
        private bool resumeLiveAfterReplay;
        private string uiError;
        public TemplateReplayState? PlaybackState => replay?.State;
        public TemplateDifference ReplayDifference => replay?.FirstDifference;
        public string RecordingPath => replayPath;
        public string ReplayPath { get => replayPath; set => replayPath = value ?? string.Empty; }
        public string ReplayStatus => replay == null ? (livePaused ? "LIVE / PAUSED" : "LIVE / RECORDING")
            : "REPLAY / " + replay.State + "  " + replay.CurrentTick + "/" + replay.EndTick;
        public string UiMessage
        {
            get
            {
                if (uiError != null) return uiError;
                if (replay?.FirstDifference != null)
                    return "DIVERGED at tick " + replay.FirstDifference.Tick + " / " + replay.FirstDifference.Category;
                if (replay?.State == TemplateReplayState.Completed)
                    return "VERIFIED / all recorded tick hashes and action results match.";
                if (replay?.State == TemplateReplayState.ReproducedFailure)
                    return "REPRODUCED FAILURE / the recorded failure fingerprint matches. This is not a replay divergence.";
                return replayMessage;
            }
        }

        public string SaveRecording()
        {
            EnsureInitialized();
            if (replay != null) throw new InvalidOperationException("Return to live mode before saving the live recording.");
            string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "ArenaRecordings");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "arena-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                TemplateRecordingIO.Write(stream, live.CaptureRecording());
            replayPath = path;
            replayMessage = "Saved " + Path.GetFileName(path) + ". Load path to replay in a fresh session.";
            Debug.Log("Arena recording saved: " + path, this);
            return path;
        }

        public void LoadReplay(string path)
        {
            EnsureInitialized();
            TemplateRecording recording;
            using (FileStream stream = File.OpenRead(path)) recording = TemplateRecordingIO.Read(stream);
            // A recording chooses among known compiled policies, never executable rules supplied by a file.
            ArenaDefinition definition = new ArenaDefinition();
            if (recording.Policy != definition.PolicyId)
            {
                definition = new ArenaDefinition(failureOracle: true);
                if (recording.Policy != definition.PolicyId)
                    throw new InvalidDataException("Unknown Arena recording policy: " + recording.Policy);
            }
            TemplateReplay<ArenaRuntime, ArenaScenario, ArenaInput, ArenaObservation> next = definition.CreateReplay(recording);
            try
            {
                if (replay == null)
                {
                    resumeLiveAfterReplay = !livePaused;
                    live.ClearInput(); live.Pause();
                }
                replay?.Dispose();
            }
            catch { next.Dispose(); throw; }
            replay = next;
            replayPath = path;
            views.SetTickDelta(recording.TickDelta);
            views.Snap(replay.Observe());
            BindDiagnostics(replay.Diagnostics);
            replayMessage = replay.Warnings.Count == 0 ? "Loaded in a fresh session. Live state is held separately; playback starts paused."
                : "Loaded with warnings: " + string.Join(", ", replay.Warnings);
        }

        public void PlayReplay()
        {
            EnsureInitialized();
            replay?.Play();
        }

        public void PauseReplay()
        {
            EnsureInitialized();
            replay?.Pause();
            if (replay != null) views.Snap(replay.Observe());
        }

        public void StepReplay()
        {
            EnsureInitialized();
            if (replay == null) return;
            replay.Step();
            views.Present(replay.PreviousObservation, replay.Observe(), 1);
            diagnostics.Poll();
        }

        public void RestartReplay()
        {
            EnsureInitialized();
            if (replay == null) return;
            replay.Restart();
            views.Snap(replay.Observe());
            BindDiagnostics(replay.Diagnostics);
            replayMessage = "Replay restarted at tick 0; presentation history was snapped.";
        }

        public void ReturnToLive()
        {
            EnsureInitialized();
            if (replay == null) return;
            replay.Dispose(); replay = null;
            live.ClearInput();
            if (resumeLiveAfterReplay) { live.Resume(); livePaused = false; }
            else livePaused = true;
            views.SetTickDelta(scenario.TickDelta);
            views.Snap(live.Observe());
            BindDiagnostics(live.Diagnostics);
            replayMessage = "Returned to the original live session. Replay time never advanced its clock.";
        }

        /// <summary>View event boundary: report a control error without stopping the simulation adapter.</summary>
        public void InvokeUi(Action action)
        {
            try { action(); uiError = null; }
            catch (Exception exception) { uiError = exception.GetType().Name + ": " + exception.Message; }
        }
    }
}
