namespace DeterministicSimulation
{
    /// <summary>Immutable tick value passed from the framework to simulation modules.</summary>
    public readonly struct SimulationTick : ISimulationTick
    {
        public SimulationTick(ulong number, float deltaTime)
        {
            Number = number;
            DeltaTime = deltaTime;
        }

        public ulong Number { get; }
        public float DeltaTime { get; }

        public override string ToString()
        {
            return $"Tick {Number} ({DeltaTime}s)";
        }
    }
}
