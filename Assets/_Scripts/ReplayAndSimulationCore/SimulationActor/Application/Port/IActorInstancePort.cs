using SimulationCore.SimulationActor.Contract;
using UnityEngine;

namespace SimulationCore.SimulationActor.Application.Port
{
    public interface IActorInstancePort
    {
        T[] CreateActorInstances<T>(int poolId, int capacity) where T : IActor;
    }
}
