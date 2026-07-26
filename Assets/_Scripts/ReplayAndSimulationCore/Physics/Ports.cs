using SimulationCore.SimulationPhysics.Contract;

namespace SimulationCore.SimulationPhysics.Application
{
    public interface ISimulationPort
    {
        void Simulate(float deltaTime);
    }
    public interface IPhysicsEventSink
    {
        void RecordCollision(CollisionFact collisionFact);
    }
}