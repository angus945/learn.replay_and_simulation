namespace DeterministicSimulation
{
    public interface ISimulationTick
    {
        ulong Number { get; }
        float DeltaTime { get; }
    }
}
