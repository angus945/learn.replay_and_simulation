using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.SimulationPhysics.Contract;

namespace SimulationCore.SimulationPhysics.Application
{
    public class CollisionEventSink : ICollisionEventSink
    {
        // TODO ordering collision facts
        List<CollisionFact> collisionFacts = new List<CollisionFact>();

        public void RecordCollision(CollisionFact collisionFact)
        {
            collisionFacts.Add(collisionFact);
            //TODO
        }
    }
    public class SimulationPhysics : ISimulationPhysics
    {
        ISimulationPort simulationPort;
        ICollisionEventSink collisionEventSink;

        public SimulationPhysics(ISimulationPort simulationPort, ICollisionEventSink collisionEventSink)
        {
            this.simulationPort = simulationPort;
            this.collisionEventSink = collisionEventSink;
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

        public void PublishPhysicsEvents()
        {
            throw new System.NotImplementedException();
        }

    }
}
