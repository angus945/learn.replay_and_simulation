using System;

namespace DeterministicSimulation.Framework
{
    /// <summary>Composition support for session adapters (including testability).
    /// Every manual Step, Reset and Dispose entry must call EnsureManual before mutating state.</summary>
    public sealed class SimulationDriveOwnership
    {
        private RealtimeSimulationRunner driver;
        public bool HasRealtimeDriver => driver != null;
        public void EnsureManual()
        {
            if (driver != null) throw new InvalidOperationException("Realtime runner owns tick authority; dispose it before manual Step, Reset or session Dispose.");
        }
        public RealtimeSimulationRunner CreateRunner(ISimulationTickSource simulation,
            int maxTicksPerFrame = 120, IRealtimeInputSource input = null, IRealtimePresentation presentation = null)
        {
            EnsureManual();
            driver = new RealtimeSimulationRunner(simulation, this, maxTicksPerFrame, input, presentation);
            return driver;
        }
        internal void Release(RealtimeSimulationRunner runner)
        {
            if (!ReferenceEquals(driver, runner)) throw new InvalidOperationException("Runner does not own this drive.");
            driver = null;
        }
    }
}
