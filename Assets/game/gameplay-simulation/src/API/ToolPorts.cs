using Testability;

namespace GameplaySimulation
{
    public interface ISimulationControl
    {
        SimulationDriveMode DriveMode { get; }
        TickReport Step();
    }

    /// <summary>Composition-only clock authority. Do not distribute to manual tools.</summary>
    public interface IRealtimeTickDriver { TickReport AdvanceTick(); }

    public interface IActionResultReader
    {
        ActionLookup Find(string sessionId, ulong sequence);
        ActionResultPage Read(string sessionId, int afterIndex, int maxItems);
    }

    public interface IGameplayCapabilities { GameplayCapabilities Describe(); }
}
