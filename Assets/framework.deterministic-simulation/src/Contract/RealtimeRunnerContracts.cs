namespace DeterministicSimulation.Framework
{
    /// <summary>Privileged session adapter, not a gameplay-facing manual Step port.
    /// PrepareTick may stop an exhausted session; it must not advance simulation state.</summary>
    public interface ISimulationTickSource
    {
        float TickDelta { get; }
        ulong TickNumber { get; }
        bool PrepareTick();
        void AdvanceTick();
    }

    /// <summary>Acquire external input for the upcoming tick, without directly advancing the session.</summary>
    public interface IRealtimeInputSource
    {
        void AcquireInput(SimulationTick tick);
    }

    /// <summary>Capture after each attempted tick; render separately using interpolation alpha.
    /// A faulted testability tick may retain an older observation; consult ObservationTick.</summary>
    public interface IRealtimePresentation
    {
        void CaptureTickState(ulong tick);
        void Render(float alpha);
    }
}
