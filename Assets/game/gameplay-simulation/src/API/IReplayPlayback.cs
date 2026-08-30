using Testability;

namespace GameplaySimulation
{
    public interface IReplayPlayback : IStateObserver<GameplayObservation>
    {
        ReplayPlaybackState State { get; }
        ulong EndTick { get; }
        RerunDifference FirstDifference { get; }
        void Play();
        void Pause();
        void Step();
        void Restart();
        void AdvanceTime(float seconds);
    }
}
