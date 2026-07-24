using UnityEngine;

namespace SimulationCore.SimulationActor.Contract
{
    public interface IActor
    {

    }

    public readonly struct ActorHandle
    {
        public int PoolId { get; }
        public int SlotId { get; }
        public uint Generation { get; }

        public ActorHandle(int poolId, int resourceId, uint generation)
        {
            PoolId = poolId;
            SlotId = resourceId;
            Generation = generation;
        }
    }
}
