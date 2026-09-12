using System;
using System.Collections.Generic;
using Arena.Integration;
using Deterministic.Playback;
using Module.Verification.Oracle;

namespace Arena.Composition
{
    public enum ArenaReplayState
    {
        Paused,
        Playing,
        Completed,
        ReproducedFailure,
        Diverged,
        Disposed
    }

    public sealed class ArenaReplayDifference
    {
        public ArenaReplayDifference(ulong tick, string category, string expected, string actual)
        {
            Tick = tick;
            Category = category;
            Expected = expected;
            Actual = actual;
        }

        public ulong Tick { get; }
        public string Category { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    /// <summary>Arena owns playback policy and file semantics; the framework owns the adapter lifecycle and step cursor.</summary>
    public sealed class ArenaReplay : IDisposable
    {
        private readonly ArenaDefinition definition;
        private readonly ArenaRecording recording;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly OracleSet<ArenaDeterminismContext> oracleSet;
        private ArenaPlaybackAdapter adapter;
        private PlaybackSession<ulong, ArenaTickEvidence, string> playback;
        private double accumulator;
        private bool busy;

        internal ArenaReplay(ArenaDefinition definition, ArenaRecording recording)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.recording = recording ?? throw new ArgumentNullException(nameof(recording));
            recording.Validate();
            List<ITestOracle<ArenaDeterminismContext>> oracles = new List<ITestOracle<ArenaDeterminismContext>>();
            oracles.Add(new ArenaDeterminismOracle());
            oracleSet = new OracleSet<ArenaDeterminismContext>(definition.PolicyId + "/determinism", oracles);
            List<string> warnings = new List<string>();
            string runtime = Environment.Version + " / " + Environment.OSVersion;
            if (!string.Equals(recording.Runtime, runtime, StringComparison.Ordinal)) warnings.Add("runtime.mismatch");
            Warnings = warnings.AsReadOnly();
            Restart();
        }

        public ArenaReplayState State { get; private set; }
        public ArenaReplayDifference FirstDifference { get; private set; }
        public IReadOnlyList<string> Warnings { get; }
        public ulong CurrentTick
        {
            get { return adapter == null ? 0 : adapter.Session.CurrentTick; }
        }
        public ulong EndTick
        {
            get { return (ulong)recording.Ticks.Count; }
        }
        public ArenaObservation PreviousObservation { get; private set; }
        public float PresentationAlpha
        {
            get
            {
                if (State != ArenaReplayState.Playing) return 1f;
                return (float)Math.Min(1, Math.Max(0, accumulator / recording.TickDelta));
            }
        }
        public IArenaDiagnosticReader Diagnostics
        {
            get
            {
                EnsureIdle();
                return adapter.Session.Diagnostics;
            }
        }

        public ArenaObservation Observe()
        {
            EnsureIdle();
            return adapter.Session.Observe();
        }

        public void Restart()
        {
            EnsureIdle();
            busy = true;
            ArenaPlaybackAdapter nextAdapter = null;
            PlaybackSession<ulong, ArenaTickEvidence, string> nextPlayback = null;
            try
            {
                nextAdapter = new ArenaPlaybackAdapter(definition, recording);
                nextPlayback = new PlaybackSession<ulong, ArenaTickEvidence, string>(nextAdapter, recording.Policy, Comparer<ulong>.Default, StringComparer.Ordinal);
                if (playback != null) playback.Dispose();
                adapter = nextAdapter;
                playback = nextPlayback;
                nextAdapter = null;
                nextPlayback = null;
                FirstDifference = null;
                accumulator = 0;
                State = ArenaReplayState.Paused;
                PreviousObservation = adapter.Session.Observe();
                CompareHeader();
                if (FirstDifference == null && recording.Ticks.Count == 0) State = ArenaReplayState.Completed;
            }
            catch
            {
                if (nextPlayback != null) nextPlayback.Dispose();
                else if (nextAdapter != null) nextAdapter.Dispose();
                State = ArenaReplayState.Diverged;
                throw;
            }
            finally
            {
                busy = false;
            }
        }

        public void Play()
        {
            EnsureIdle();
            if (State == ArenaReplayState.Paused) State = ArenaReplayState.Playing;
        }

        public void Pause()
        {
            EnsureIdle();
            if (State == ArenaReplayState.Playing) State = ArenaReplayState.Paused;
            accumulator = 0;
        }

        public void Step()
        {
            EnsureIdle();
            if (State != ArenaReplayState.Paused) throw new InvalidOperationException("Pause Arena replay before single stepping.");
            busy = true;
            try
            {
                Tick();
                accumulator = 0;
            }
            finally
            {
                busy = false;
            }
        }

        public void AdvanceTime(float seconds)
        {
            EnsureIdle();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != ArenaReplayState.Playing) return;
            busy = true;
            try
            {
                accumulator += seconds;
                int budget = 120;
                while (accumulator >= recording.TickDelta && State == ArenaReplayState.Playing && budget > 0)
                {
                    accumulator -= recording.TickDelta;
                    Tick();
                    budget--;
                }
            }
            finally
            {
                busy = false;
            }
        }

        public void Dispose()
        {
            if (State == ArenaReplayState.Disposed) return;
            EnsureIdle();
            State = ArenaReplayState.Disposed;
            playback.Dispose();
        }

        private void Tick()
        {
            if (CurrentTick >= EndTick) return;
            PreviousObservation = adapter.Session.Observe();
            PlaybackRecord<ulong, ArenaTickEvidence, string> record = playback.Step();
            ArenaTickEvidence actual = record.Evidence;
            ArenaRecordedTick expectedTick = recording.Ticks[(int)actual.Tick - 1];
            ArenaTickEvidence expected = expectedTick.ToEvidence();
            EvaluationReport report = oracleSet.Evaluate("tick:" + actual.Tick, new ArenaDeterminismContext(expected, actual));
            if (report.Verdict != TestVerdict.Passed)
            {
                OracleResult result = report.Results.Count == 0 ? null : report.Results[0];
                string category = result == null ? "oracle.infrastructure" : result.Detail;
                FirstDifference = new ArenaReplayDifference(actual.Tick, category, expected.Digest, actual.Digest);
                State = ArenaReplayState.Diverged;
                return;
            }
            if (actual.Tick == EndTick) State = expectedTick.Failure == null ? ArenaReplayState.Completed : ArenaReplayState.ReproducedFailure;
        }

        private void CompareHeader()
        {
            if (!string.Equals(recording.Policy, definition.PolicyId, StringComparison.Ordinal))
            {
                FirstDifference = new ArenaReplayDifference(0, "policy", recording.Policy, definition.PolicyId);
                State = ArenaReplayState.Diverged;
                return;
            }
            if (!string.Equals(recording.InitialDigest, adapter.Session.InitialDigest, StringComparison.Ordinal))
            {
                FirstDifference = new ArenaReplayDifference(0, "initial_digest", recording.InitialDigest, adapter.Session.InitialDigest);
                State = ArenaReplayState.Diverged;
            }
        }

        private void EnsureIdle()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Use the Arena replay owner thread.");
            if (State == ArenaReplayState.Disposed) throw new ObjectDisposedException(nameof(ArenaReplay));
            if (busy) throw new InvalidOperationException("Arena replay callbacks cannot reenter the host.");
        }
    }
}
