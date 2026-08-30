using SimulationCore.World.Contract;

namespace SimulationCore.SimulationPhysics.Contract
{
    public enum ContactPhase : byte
    {
        Enter,
        Stay,
        Exit
    }
    public readonly struct CollisionFact
    {
        public readonly EntityHandle EntityA;
        public readonly EntityHandle EntityB;
        public readonly ContactPhase Phase;

        public CollisionFact(EntityHandle entityA, EntityHandle entityB, ContactPhase phase)
        {
            if (CompareEntityHandles(entityA, entityB) <= 0)
            {
                EntityA = entityA;
                EntityB = entityB;
            }
            else
            {
                EntityA = entityB;
                EntityB = entityA;
            }

            Phase = phase;
        }

        private static int CompareEntityHandles(EntityHandle left, EntityHandle right)
        {
            int result = left.SequenceId.CompareTo(right.SequenceId);

            if (result != 0)
                return result;

            return left.SlotId.CompareTo(right.SlotId);
        }
    }
}