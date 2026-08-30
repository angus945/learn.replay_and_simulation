using System;
using System.Collections.Generic;

namespace SimulationObjects.Contract
{
    public enum SimulationObjectState
    {
        Invalid,
        PendingSpawn,
        Alive,
        PendingDestroy
    }

    /// <summary>Immutable observation, not a live view. SpawnSequence is zero until first commit.</summary>
    public readonly struct SimulationObjectRecord
    {
        internal SimulationObjectRecord(SimulationObjectId id, SimulationObjectHandle handle,
            ulong spawnSequence, SimulationObjectState state)
        {
            Id = id;
            Handle = handle;
            SpawnSequence = spawnSequence;
            State = state;
        }

        public SimulationObjectId Id { get; }
        public SimulationObjectHandle Handle { get; }
        public ulong SpawnSequence { get; }
        public SimulationObjectState State { get; }
        public bool IsActive => SpawnSequence != 0 &&
            (State == SimulationObjectState.Alive || State == SimulationObjectState.PendingDestroy);
    }

    /// <summary>
    /// Destroys/cancellations are applied before spawns. Each list is ordered by stable object ID.
    /// Removed records describe their last PendingDestroy state; spawned records describe Alive state.
    /// </summary>
    public sealed class StructuralCommitResult
    {
        internal StructuralCommitResult(SimulationObjectRecord[] spawned,
            SimulationObjectRecord[] destroyed, SimulationObjectRecord[] cancelled)
        {
            Spawned = Array.AsReadOnly(spawned);
            Destroyed = Array.AsReadOnly(destroyed);
            CancelledSpawns = Array.AsReadOnly(cancelled);
        }

        public IReadOnlyList<SimulationObjectRecord> Spawned { get; }
        public IReadOnlyList<SimulationObjectRecord> Destroyed { get; }
        public IReadOnlyList<SimulationObjectRecord> CancelledSpawns { get; }
    }
}
