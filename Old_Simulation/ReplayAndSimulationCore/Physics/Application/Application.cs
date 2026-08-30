using SimulationCore.Contracts;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationPhysics.Contract
{
    public struct OnCollisionEnter : IEvent
    {
        public readonly EntityHandle EntityA;
        public readonly EntityHandle EntityB;

        public OnCollisionEnter(EntityHandle entityA, EntityHandle entityB)
        {
            EntityA = entityA;
            EntityB = entityB;
        }
    }
    public struct OnCollisionStay : IEvent
    {
        public readonly EntityHandle EntityA;
        public readonly EntityHandle EntityB;

        public OnCollisionStay(EntityHandle entityA, EntityHandle entityB)
        {
            EntityA = entityA;
            EntityB = entityB;
        }
    }
    public struct OnCollisionExit : IEvent
    {
        public readonly EntityHandle EntityA;
        public readonly EntityHandle EntityB;

        public OnCollisionExit(EntityHandle entityA, EntityHandle entityB)
        {
            EntityA = entityA;
            EntityB = entityB;
        }
    }
}

namespace SimulationCore.SimulationPhysics.Application
{
    public interface IPhysicsEventPort
    {
        void PublishCollisionEvents(ulong tick);
    }

    public class SimulationPhysics : ISimulationPhysics
    {
        ISimulationPort simulationPort;
        IPhysicsEventPort eventPort;

        public SimulationPhysics(ISimulationPort simulationPort, IPhysicsEventPort eventPort)
        {
            this.simulationPort = simulationPort;
            this.eventPort = eventPort;
        }

        public void ApplyPrePhysicsState()
        {
            // TODO
        }

        public void Simulate(float deltaTime)
        {
            simulationPort.Simulate(deltaTime);
        }

        public void CapturePostPhysicsState()
        {
            // TODO
        }

        public void PublishPhysicsEvents(ulong tick)
        {
            eventPort.PublishCollisionEvents(tick);
        }

    }
}
