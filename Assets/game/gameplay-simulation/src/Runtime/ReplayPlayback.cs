using System;
using System.Collections.Generic;
using Testability;

namespace GameplaySimulation
{
    /// <summary>Project-specific, single-threaded playback. Owns a fresh manual session; never exposes Submit.</summary>
    public sealed class ReplayPlayback : IReplayPlayback
    {
        private readonly ReplayArtifact artifact;
        private readonly Func<GameplaySession> factory;
        private GameplaySession session;
        private double accumulator;
        private int resultIndex;
        private GameplayObservation current;
        public ReplayPlayback(ReplayArtifact artifact, Func<GameplaySession> factory = null, string currentBuild = null)
        {
            this.artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            artifact.Validate();
            this.factory = factory ?? (() => new GameplaySession());
            List<string> warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(currentBuild)) warnings.Add("build.unverified");
            else if (currentBuild != artifact.Scenario.Build) warnings.Add("build.mismatch");
            if (artifact.Runtime != Environment.Version + " / " + Environment.OSVersion) warnings.Add("runtime.mismatch");
            Warnings = warnings.AsReadOnly();
            Restart();
        }
        public IReadOnlyList<string> Warnings { get; }
        public ReplayPlaybackState State { get; private set; }
        public ulong EndTick => artifact.EndTick;
        public RerunDifference FirstDifference { get; private set; }
        public IDiagnosticReader<GameplayObservation> Diagnostics => session.Diagnostics;
        public GameplayObservation PreviousObservation { get; private set; }
        public float PresentationAlpha => State == ReplayPlaybackState.Playing ? (float)Math.Min(1, accumulator / artifact.Scenario.TickDelta) : 1;
        public GameplayObservation Observe() => current;
        public void Restart()
        {
            GameplaySession next = factory();
            if (next == null || next.State != SessionState.Created || next.DriveMode != SimulationDriveMode.Manual)
                throw new InvalidOperationException("Replay requires a fresh manual session.");
            next.Admin.Start(artifact.Scenario);
            foreach (GameplayRequest action in artifact.Actions)
            {
                SubmissionResult result = next.Gameplay.Submit(action.InSession(next.Id));
                if (!result.Queued) throw new ArgumentException("Replay admission: " + result.Code);
            }
            session = next; accumulator = 0; resultIndex = 0; FirstDifference = null;
            current = session.Gameplay.Observe(); PreviousObservation = current; State = ReplayPlaybackState.Paused;
            Compare("policy", 0, artifact.DiagnosticPolicy, session.DiagnosticPolicy);
            Compare("state_hash", 0, artifact.Hashes[0].Hash, session.HashHistory[0].Hash);
            if (State != ReplayPlaybackState.Diverged && EndTick == 0) State = ReplayPlaybackState.Completed;
        }
        public void Play() { if (State == ReplayPlaybackState.Paused) State = ReplayPlaybackState.Playing; }
        public void Pause()
        {
            if (State == ReplayPlaybackState.Playing) State = ReplayPlaybackState.Paused;
            accumulator = 0; PreviousObservation = current;
        }
        public void Step()
        {
            if (State != ReplayPlaybackState.Paused) throw new InvalidOperationException("Single step requires paused playback.");
            Tick(); PreviousObservation = current; accumulator = 0;
        }
        public void AdvanceTime(float seconds)
        {
            if (!GameplayScenario.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != ReplayPlaybackState.Playing) return;
            accumulator += seconds;
            // Bound work per rendered frame, retaining backlog rather than dropping simulation time.
            int budget = 120;
            while (accumulator >= artifact.Scenario.TickDelta && State == ReplayPlaybackState.Playing && budget-- > 0)
            { accumulator -= artifact.Scenario.TickDelta; Tick(); }
        }
        private void Tick()
        {
            TickReport report = session.Simulation.Step();
            PreviousObservation = current; current = session.Gameplay.Observe();
            foreach (ActionResult actual in report.Results)
            {
                ActionResult expected = resultIndex < artifact.Results.Count ? artifact.Results[resultIndex] : null;
                Compare("action_result", report.Tick, Format(expected), Format(actual)); resultIndex++;
            }
            if (resultIndex < artifact.Results.Count && artifact.Results[resultIndex].Tick <= report.Tick)
                Compare("action_result", report.Tick, Format(artifact.Results[resultIndex]), null);
            Compare("state_hash", report.Tick, artifact.Hashes[(int)report.Tick].Hash, report.StateHash);
            if (session.State == SessionState.Faulted) Compare("session.failure", report.Tick, null, session.Failure.Code);
            if (State != ReplayPlaybackState.Diverged && report.Tick == EndTick) State = ReplayPlaybackState.Completed;
        }
        private void Compare(string category, ulong tick, string expected, string actual)
        {
            if (expected == actual || FirstDifference != null) return;
            FirstDifference = new RerunDifference(category, tick, expected, actual); State = ReplayPlaybackState.Diverged;
        }
        private static string Format(ActionResult result) => result == null ? null : result.Tick + ":" + result.Sequence + ":" + result.Status + ":" + result.Code;
    }
}
