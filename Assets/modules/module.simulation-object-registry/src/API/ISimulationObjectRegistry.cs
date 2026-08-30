using System.Collections.Generic;
using SimulationObjects.Contract;

namespace SimulationObjects
{
    /// <summary>One registry per simulation session; caller owns Commit timing.</summary>
    public interface ISimulationObjectRegistry
    {
        SimulationObjectRecord RequestSpawn();
        bool RequestDestroy(SimulationObjectHandle handle);
        bool TryGet(SimulationObjectHandle handle, out SimulationObjectRecord record);
        bool TryGet(SimulationObjectId id, out SimulationObjectRecord record);
        IReadOnlyList<SimulationObjectRecord> GetActiveOrdered();
        IReadOnlyList<SimulationObjectRecord> GetObjectsOrdered();
        StructuralCommitResult Commit();
    }
}
