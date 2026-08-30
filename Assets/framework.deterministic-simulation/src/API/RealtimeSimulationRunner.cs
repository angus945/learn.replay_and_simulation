using System;

namespace DeterministicSimulation.Framework
{
    /// <summary>Owner-thread frame clock. Retains catch-up debt; never owns/disposes the session.
    /// Dispose releases exclusive tick authority. Pause keeps authority and discards frame debt.</summary>
    public sealed class RealtimeSimulationRunner : IDisposable
    {
        private readonly ISimulationTickSource simulation;
        private readonly IRealtimeInputSource input;
        private readonly IRealtimePresentation presentation;
        private readonly SimulationDriveOwnership ownership;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private double accumulator;
        private bool busy, disposed;

        internal RealtimeSimulationRunner(ISimulationTickSource simulation, SimulationDriveOwnership ownership,
            int maxTicksPerFrame, IRealtimeInputSource input, IRealtimePresentation presentation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            float tickDelta = simulation.TickDelta;
            if (float.IsNaN(tickDelta) || float.IsInfinity(tickDelta) || tickDelta <= 0) throw new ArgumentOutOfRangeException(nameof(tickDelta));
            if (maxTicksPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(maxTicksPerFrame));
            TickDelta = tickDelta; MaxTicksPerFrame = maxTicksPerFrame;
            this.simulation = simulation; this.ownership = ownership;
            this.input = input; this.presentation = presentation;
        }
        public float TickDelta { get; }
        public int MaxTicksPerFrame { get; }
        public bool IsPaused { get; private set; }
        public Exception Failure { get; private set; }
        public double PendingSeconds => accumulator;
        public float PresentationAlpha => (float)Math.Min(1, accumulator / TickDelta);

        public int AdvanceTime(float seconds)
        {
            EnsureIdle();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (Failure != null) throw new InvalidOperationException("Dispose the failed runner before creating a new driver.", Failure);
            if (IsPaused) return 0;
            busy = true;
            int advanced = 0;
            try
            {
                if (!simulation.PrepareTick()) { accumulator = 0; return 0; }
                accumulator += seconds;
                while (accumulator >= TickDelta && advanced < MaxTicksPerFrame)
                {
                    if (!simulation.PrepareTick()) { accumulator = 0; break; }
                    input?.AcquireInput(new SimulationTick(checked(simulation.TickNumber + 1), TickDelta));
                    if (!simulation.PrepareTick()) { accumulator = 0; break; }
                    accumulator -= TickDelta; // A failed/partial tick is never retried automatically.
                    simulation.AdvanceTick(); advanced++;
                    presentation?.CaptureTickState(simulation.TickNumber);
                    if (!simulation.PrepareTick()) { accumulator = 0; break; }
                }
                return advanced;
            }
            catch (Exception error)
            { Failure = error; IsPaused = true; accumulator = 0; throw; }
            finally { busy = false; }
        }
        public void Pause() { EnsureIdle(); IsPaused = true; accumulator = 0; }
        public void UpdatePresentation()
        {
            EnsureIdle();
            if (Failure != null) throw new InvalidOperationException("Cannot render through a failed runner.", Failure);
            busy = true;
            try { presentation?.Render(PresentationAlpha); }
            catch (Exception error) { Failure = error; IsPaused = true; accumulator = 0; throw; }
            finally { busy = false; }
        }
        public void Resume()
        {
            EnsureIdle();
            if (Failure != null) throw new InvalidOperationException("Cannot resume a failed runner.", Failure);
            IsPaused = false;
        }
        public void Dispose()
        {
            if (disposed) return;
            EnsureIdle(); disposed = true; accumulator = 0; ownership.Release(this);
        }
        private void EnsureIdle()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Use the runner owner thread.");
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (busy) throw new InvalidOperationException("Runner callbacks cannot reenter or release the driver.");
        }
    }
}
