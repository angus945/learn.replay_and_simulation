using System;
using System.Collections.Generic;

namespace Testability.Templates
{
    public enum TemplateReplayState { Paused, Playing, Completed, ReproducedFailure, Diverged, Disposed }

    public sealed class TemplateDifference
    {
        internal TemplateDifference(ulong tick, string category, string expected, string actual)
        { Tick = tick; Category = category; Expected = expected; Actual = actual; }
        public ulong Tick { get; }
        public string Category { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    /// <summary>Replays only recorded external inputs in a fresh manual session. No live Submit surface.</summary>
    public sealed class TemplateReplay<TWorld, TScenario, TInput, TObservation> : IDisposable where TWorld : class
    {
        private readonly ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition;
        private readonly TemplateRecording recording;
        private TestableSimulationSession<TWorld, TScenario, TInput, TObservation> session;
        private double accumulator;
        private bool busy;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal TemplateReplay(ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation> definition, TemplateRecording recording)
        {
            this.definition = definition;
            this.recording = recording ?? throw new ArgumentNullException(nameof(recording));
            recording.Validate();
            List<string> warnings = new List<string>();
            if (recording.Runtime != Environment.Version + " / " + Environment.OSVersion) warnings.Add("runtime.mismatch");
            Warnings = warnings.AsReadOnly(); Restart();
        }
        public TemplateReplayState State { get; private set; }
        public TemplateDifference FirstDifference { get; private set; }
        public IReadOnlyList<string> Warnings { get; }
        public ulong CurrentTick => session == null ? 0 : session.CurrentTick;
        public ulong EndTick => (ulong)recording.Ticks.Count;
        public TObservation PreviousObservation { get; private set; }
        public float PresentationAlpha => State == TemplateReplayState.Playing
            ? (float)Math.Min(1, Math.Max(0, accumulator / recording.TickDelta)) : 1f;
        public IDiagnosticReader<TObservation> Diagnostics { get { EnsureIdle(); return session.Diagnostics; } }
        public TObservation Observe() { EnsureIdle(); return session.Observe(); }

        public void Restart()
        {
            EnsureIdle(); busy = true;
            try
            {
                FirstDifference = null; accumulator = 0; State = TemplateReplayState.Paused;
                TestableSimulationSession<TWorld, TScenario, TInput, TObservation> next =
                    definition.CreateTestSession(definition.LoadScenario(recording.Scenario), recording.Limits);
                try
                {
                    foreach (RecordedInput input in recording.Inputs)
                    {
                        SubmissionResult admission = next.Submit(next.Id, input.Sequence, input.Tick, definition.LoadInput(input.Payload));
                        if (!admission.Queued) throw new ArgumentException("Replay input admission failed: " + admission.Code);
                    }
                    session?.Dispose();
                }
                catch { next.Dispose(); throw; }
                session = next;
                PreviousObservation = session.Observe();
                TemplateRecording initial = session.CaptureRecording();
                Compare(0, "policy", recording.Policy, initial.Policy);
                Compare(0, "tick_delta", recording.TickDelta.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    initial.TickDelta.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Compare(0, "initial_hash", recording.InitialHash, initial.InitialHash);
                if (State != TemplateReplayState.Diverged && recording.Ticks.Count == 0) State = TemplateReplayState.Completed;
            }
            catch { State = TemplateReplayState.Diverged; throw; }
            finally { busy = false; }
        }
        public void Play() { EnsureIdle(); if (State == TemplateReplayState.Paused) State = TemplateReplayState.Playing; }
        public void Pause() { EnsureIdle(); if (State == TemplateReplayState.Playing) State = TemplateReplayState.Paused; accumulator = 0; }
        public void Step()
        {
            EnsureIdle();
            if (State != TemplateReplayState.Paused) throw new InvalidOperationException("Pause before single stepping.");
            busy = true;
            try { Tick(); accumulator = 0; }
            finally { busy = false; }
        }
        public void AdvanceTime(float seconds)
        {
            EnsureIdle();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != TemplateReplayState.Playing) return;
            busy = true;
            try
            {
                accumulator += seconds;
                int budget = 120;
                while (accumulator >= recording.TickDelta && State == TemplateReplayState.Playing && budget-- > 0)
                { accumulator -= recording.TickDelta; Tick(); }
            }
            finally { busy = false; }
        }
        private void Tick()
        {
            TemplateTick expected = recording.Ticks[(int)session.CurrentTick];
            PreviousObservation = session.Observe();
            TemplateTick actual = session.Step();
            Compare(actual.Tick, "state_hash", expected.Hash, actual.Hash);
            Compare(actual.Tick, "result_count", expected.Results.Count.ToString(), actual.Results.Count.ToString());
            for (int i = 0; i < Math.Min(expected.Results.Count, actual.Results.Count); i++)
                Compare(actual.Tick, "action_result", Format(expected.Results[i]), Format(actual.Results[i]));
            TemplateFailure expectedFailure = recording.Failure != null && recording.Failure.Tick == actual.Tick ? recording.Failure : null;
            Compare(actual.Tick, "failure", expectedFailure?.Fingerprint, session.Failure?.Fingerprint);
            if (State != TemplateReplayState.Diverged && actual.Tick == (ulong)recording.Ticks.Count)
                State = expectedFailure == null ? TemplateReplayState.Completed : TemplateReplayState.ReproducedFailure;
        }
        private void Compare(ulong tick, string category, string expected, string actual)
        {
            if (expected == actual || FirstDifference != null) return;
            FirstDifference = new TemplateDifference(tick, category, expected, actual); State = TemplateReplayState.Diverged;
        }
        private static string Format(ActionResult result) => result.Sequence + ":" + result.Tick + ":" + result.Status + ":" + result.Code;
        public void Dispose()
        {
            if (State == TemplateReplayState.Disposed) return;
            EnsureIdle(); State = TemplateReplayState.Disposed; session?.Dispose();
        }
        private void EnsureIdle()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Use the replay owner thread.");
            if (State == TemplateReplayState.Disposed) throw new ObjectDisposedException(GetType().Name);
            if (busy) throw new InvalidOperationException("Replay callback reentry is not allowed.");
        }
    }
}
