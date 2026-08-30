using System;

namespace GameplaySimulation
{
    public sealed partial class GameplaySession
    {
        /// <summary>Immutable recording of tick zero through the current successful boundary. Does not stop the session.</summary>
        public ReplayArtifact CaptureReplay()
        {
            EnsureIdle();
            if (stepping || scenario == null || State == Testability.SessionState.Faulted)
                throw new InvalidOperationException("Capture replay between successful ticks; use FailureArtifact for faults.");
            return new ReplayArtifact(scenario, DiagnosticPolicy, CurrentTick, history, resultHistory, hashHistory);
        }
    }
}
