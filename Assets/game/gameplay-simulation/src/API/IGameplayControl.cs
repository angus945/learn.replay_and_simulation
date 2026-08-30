using Testability;

namespace GameplaySimulation
{
    /// <summary>Gameplay-only port; intentionally excludes Start/Reset/Stop and state setters.</summary>
    public interface IGameplayControl : IStateObserver<GameplayObservation>
    {
        string Id { get; }
        ulong CurrentTick { get; }
        SubmissionResult Submit(GameplayRequest request);
    }
}
