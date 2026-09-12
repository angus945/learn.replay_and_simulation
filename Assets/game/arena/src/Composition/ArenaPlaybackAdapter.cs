using System;
using System.Collections.Generic;
using Arena.Integration;
using Deterministic.Playback;
using RuntimeControl;

namespace Arena.Composition
{
    /// <summary>Adapts the Arena-owned session to domain-neutral playback lifecycle control.</summary>
    internal sealed class ArenaPlaybackAdapter : IPlaybackAdapter<ulong, ArenaTickEvidence, string>
    {
        private readonly ArenaDefinition definition;
        private readonly ArenaRecording recording;
        private ArenaSession session;
        private bool disposed;

        public ArenaPlaybackAdapter(ArenaDefinition definition, ArenaRecording recording)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.recording = recording ?? throw new ArgumentNullException(nameof(recording));
            RecreateSession();
        }

        public ArenaSession Session
        {
            get { return session; }
        }
        public ulong InitialCoordinate
        {
            get { return 0; }
        }
        public ulong Coordinate
        {
            get { return session.CurrentTick; }
        }
        public bool HasRunnableWork
        {
            get { return session.CurrentTick < (ulong)recording.Ticks.Count && session.State == ArenaSessionState.Running; }
        }
        public bool IsTerminal
        {
            get { return session.CurrentTick >= (ulong)recording.Ticks.Count || session.State != ArenaSessionState.Running; }
        }
        public Exception CurrentFault
        {
            get { return null; }
        }
        public bool CanCaptureCheckpoint
        {
            get { return false; }
        }

        public void Initialize(PlaybackContext<ulong, string> context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
        }

        public bool TryGetNextBoundary(out ulong coordinate)
        {
            if (!HasRunnableWork)
            {
                coordinate = Coordinate;
                return false;
            }
            coordinate = Coordinate + 1;
            return true;
        }

        public void AdvanceTo(ulong coordinate)
        {
            if (coordinate != Coordinate) throw new InvalidOperationException("Arena playback advances only at recorded evidence boundaries.");
        }

        public ArenaTickEvidence Pump()
        {
            if (!HasRunnableWork) return null;
            return session.Step();
        }

        public void NotifyInputAvailable()
        {
        }

        public long? StartedScope(ArenaTickEvidence evidence)
        {
            return null;
        }

        public long? CompletedScope(ArenaTickEvidence evidence)
        {
            return null;
        }

        public bool IsCheckpointBoundary(ArenaTickEvidence evidence)
        {
            return false;
        }

        public Exception GetFault(ArenaTickEvidence evidence)
        {
            return null;
        }

        public bool EvidenceEquals(ArenaTickEvidence expected, ArenaTickEvidence actual)
        {
            return ArenaDeterminismOracle.Compare(expected, actual) == null;
        }

        public PlaybackStateSnapshot Capture()
        {
            throw new NotSupportedException("Arena playback does not publish checkpoints.");
        }

        public void Restore(object state)
        {
            throw new NotSupportedException("Arena playback does not publish checkpoints.");
        }

        public void Reset()
        {
            RecreateSession();
        }

        public void Stop()
        {
            if (session != null && session.State == ArenaSessionState.Running) session.Stop();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (session != null) session.Dispose();
        }

        private void RecreateSession()
        {
            ArenaScenario scenario = definition.DecodeScenario(recording.Scenario);
            ArenaSession next = definition.CreateSession(scenario, recording.Limits);
            try
            {
                foreach (ArenaRecordedInput input in recording.Inputs)
                {
                    ArenaInput value = definition.DecodeInput(input.Payload);
                    OperationAdmission admission = next.Submit(value, input.Tick);
                    if (!admission.IsAdmitted || admission.Handle.Sequence != input.Sequence) throw new ArgumentException("Arena replay input admission differs from the recording.");
                }
            }
            catch
            {
                next.Dispose();
                throw;
            }
            if (session != null) session.Dispose();
            session = next;
        }
    }
}
