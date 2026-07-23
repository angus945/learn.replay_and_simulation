namespace ECSManagement.Contract
{
    public readonly struct EntityHandle
    {
        public readonly int Id;
        public readonly uint Generation;
        public readonly ulong SpawnSequence;

        public EntityHandle(int id, uint generation, ulong spawnSequence)
        {
            Id = id;
            Generation = generation;
            SpawnSequence = spawnSequence;
        }

        public override string ToString()
        {
            return $"{Id}:{Generation}:{SpawnSequence}";
        }

        public override bool Equals(object obj)
        {
            return obj is EntityHandle handle &&
                   Id == handle.Id &&
                   Generation == handle.Generation &&
                   SpawnSequence == handle.SpawnSequence;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Id;
                hash = hash * 31 + Generation.GetHashCode();
                hash = hash * 31 + SpawnSequence.GetHashCode();
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
