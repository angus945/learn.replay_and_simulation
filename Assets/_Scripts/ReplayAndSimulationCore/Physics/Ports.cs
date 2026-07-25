using SimulationCore.SimulationPhysics.Contract;

namespace SimulationCore.SimulationPhysics.Application
{
    public interface ISimulationPort
    {
        void Simulate(float deltaTime);
    }
    public interface ICollisionEventSink
    {
        void RecordCollision(CollisionFact collisionFact);
    }
}