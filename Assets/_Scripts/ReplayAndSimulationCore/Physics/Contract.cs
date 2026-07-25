using SimulationCore.World.Contract;

namespace SimulationCore.SimulationPhysics.Contract
{
    public enum ContactPhase : byte
    {
        Enter,
        Stay,
        Exit
    }
    public struct CollisionFact
    {
        public readonly EntityHandle EntityA;
        public readonly EntityHandle EntityB;
        public readonly ContactPhase Phase;

        public CollisionFact(EntityHandle entityA, EntityHandle entityB, ContactPhase phase)
        {
            EntityA = entityA;
            EntityB = entityB;
            Phase = phase;
        }
    }
}