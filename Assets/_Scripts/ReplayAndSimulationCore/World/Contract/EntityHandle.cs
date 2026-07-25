namespace SimulationCore.World.Contract
{
    public readonly struct EntityHandle
    {
        public static readonly EntityHandle NotEntity = new EntityHandle(-1, 0);

        public readonly int SlotId;
        public readonly ulong SequenceId;

        public EntityHandle(int id, ulong spawnSequence)
        {
            SlotId = id;
            SequenceId = spawnSequence;
        }

        public override string ToString()
        {
            return $"{SlotId}:{SequenceId}";
        }

        public override bool Equals(object obj)
        {
            return obj is EntityHandle handle &&
                   SlotId == handle.SlotId &&
                   SequenceId == handle.SequenceId;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + SlotId;
                hash = hash * 31 + SequenceId.GetHashCode();
                return hash;
            }
        }
        public static bool operator ==(EntityHandle left, EntityHandle right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(EntityHandle left, EntityHandle right)
        {
            return !(left == right);
        }
    }
}
