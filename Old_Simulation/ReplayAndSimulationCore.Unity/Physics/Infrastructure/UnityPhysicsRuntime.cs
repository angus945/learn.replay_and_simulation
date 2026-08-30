using UnityEngine;
using SimulationCore.SimulationPhysics.Application;
namespace SimulationCore.Unity.PhysicsRuntime.Infrastructure
{
    public class UnityPhysicsRuntime : ISimulationPort
    {
        public UnityPhysicsRuntime()
        {
            Physics.simulationMode = SimulationMode.Script;
        }
        public void Simulate(float deltaTime)
        {
            Physics.SyncTransforms();
            Physics.Simulate(deltaTime);
        }
    }
}
