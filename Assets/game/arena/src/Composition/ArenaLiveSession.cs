using System;
using Arena.Application;
using Arena.Domain;
using Arena.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using RuntimeControl;
using TickInputBuffering;
using TickInputBuffering.Contract;

namespace Arena.Composition
{
    /// <summary>Frame adapter only. Does not contain gameplay rules or its own clock.</summary>
    public sealed class ArenaLiveSession : IDisposable, IRealtimeInputSource, IRealtimePresentation
    {
        private TickInputBuffer input = CreateInputBuffer();
        private readonly ArenaSession session;
        private readonly RealtimeSimulationRunner runner;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private bool disposed;
        private bool inputDirty;
        public ArenaLiveSession(ArenaScenario scenario = null)
        {
            session = new ArenaDefinition().CreateSession(scenario ?? new ArenaScenario());
            PreviousObservation = session.Observe();
            CurrentObservation = PreviousObservation;
            runner = session.CreateRealtimeRunner(input: this, presentation: this);
        }
        public ArenaObservation PreviousObservation { get; private set; }
        private ArenaObservation CurrentObservation { get; set; }
        public ulong TickNumber
        {
            get { return session.CurrentTick; }
        }
        public float PresentationAlpha
        {
            get { return runner.IsPaused || session.State != ArenaSessionState.Running ? 1f : runner.PresentationAlpha; }
        }
        public ArenaSessionState State
        {
            get { return session.State; }
        }
        public ArenaFailure Failure
        {
            get { return session.Failure; }
        }
        public Exception DriverFailure
        {
            get { return runner.Failure; }
        }
        public bool IsPaused
        {
            get { return runner.IsPaused; }
        }
        public double PendingSeconds
        {
            get { return runner.PendingSeconds; }
        }
        public IArenaDiagnosticReader Diagnostics
        {
            get { return session.Diagnostics; }
        }

        public ArenaObservation Observe()
        {
            return session.Observe();
        }

        public ArenaRecording CaptureRecording()
        {
            return session.CaptureRecording();
        }
        public void CaptureAxes(float x, float y)
        {
            EnsureInputAccess();
            if (!Position.IsFinite(x) || !Position.IsFinite(y)) throw new ArgumentException("Input must be finite.");
            input.CaptureAxis(0, x); input.CaptureAxis(1, y);
            inputDirty = true;
        }
        public void CaptureAttack(bool down) { EnsureInputAccess(); input.CaptureButton(0, down); inputDirty = true; }
        public void ClearInput()
        {
            EnsureInputAccess();
            if (!inputDirty) return;
            // A buffer has no Reset operation. Replacing this host-owned buffer discards stale
            // edges without swallowing a fresh press captured before the next simulation tick.
            input = CreateInputBuffer();
            inputDirty = false;
        }
        private static TickInputBuffer CreateInputBuffer()
        {
            TickInputBuffer buffer = new TickInputBuffer();
            buffer.RegisterAxis(0); buffer.RegisterAxis(1); buffer.RegisterButton(0); buffer.Seal();
            return buffer;
        }
        public void AdvanceTime(float seconds)
        {
            runner.AdvanceTime(seconds);
        }

        public void UpdatePresentation()
        {
            runner.UpdatePresentation();
        }
        public void Pause() { runner.Pause(); ClearInput(); }
        public void Resume()
        {
            runner.Resume();
        }

        public void Stop()
        {
            session.Stop();
        }
        public void Dispose()
        {
            if (disposed) return;
            EnsureInputAccess();
            runner.Dispose(); session.Dispose(); disposed = true;
        }
        private void EnsureInputAccess()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Use the Arena live session owner thread.");
            if (disposed) throw new ObjectDisposedException(nameof(ArenaLiveSession));
        }
        void IRealtimeInputSource.AcquireInput(SimulationTick tick)
        {
            TickInputFrame frame = input.ConsumeTick(tick.Number);
            ArenaObservation state = session.Observe();
            Submit(tick.Number, new ArenaInput(ArenaAction.Move, state.PlayerId, x: frame.GetAxis(0).Value, y: frame.GetAxis(1).Value));
            if (frame.GetButton(0).Pressed)
            {
                ulong target = 0;
                ActorSnapshot player = state.FindActor(state.PlayerId);
                double nearest = double.MaxValue;
                foreach (ActorSnapshot actor in state.Actors)
                {
                    if (!actor.Enemy || player == null) continue;
                    double dx = (double)actor.X - player.X, dy = (double)actor.Y - player.Y;
                    double distance = dx * dx + dy * dy;
                    if (distance < nearest) { nearest = distance; target = actor.Id; }
                }
                Submit(tick.Number, new ArenaInput(ArenaAction.Attack, state.PlayerId, target));
            }
        }
        private void Submit(ulong tick, ArenaInput value)
        {
            OperationAdmission admission = session.Submit(value, tick);
            if (!admission.IsAdmitted) session.Stop();
        }
        void IRealtimePresentation.CaptureTickState(ulong tick)
        { PreviousObservation = CurrentObservation; CurrentObservation = session.Observe(); }
        void IRealtimePresentation.Render(float alpha) { } // Unity reads the immutable pair; it never receives the world.
    }
}
