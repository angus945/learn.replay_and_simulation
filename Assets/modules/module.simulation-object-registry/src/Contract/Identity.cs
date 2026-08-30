using System;

namespace SimulationObjects.Contract
{
    /// <summary>Session-wide stable identity. Zero/default is invalid; IDs are never reused.</summary>
    public readonly struct SimulationObjectId : IEquatable<SimulationObjectId>, IComparable<SimulationObjectId>
    {
        public SimulationObjectId(ulong value)
        {
            if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(SimulationObjectId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SimulationObjectId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(SimulationObjectId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString();
        public static bool operator ==(SimulationObjectId left, SimulationObjectId right) => left.Equals(right);
        public static bool operator !=(SimulationObjectId left, SimulationObjectId right) => !left.Equals(right);
    }

    /// <summary>Registry-local fast reference. Generation zero/default is invalid.</summary>
    public readonly struct SimulationObjectHandle : IEquatable<SimulationObjectHandle>
    {
        public SimulationObjectHandle(int slot, uint generation)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Slot = slot;
            Generation = generation;
        }

        public int Slot { get; }
        public uint Generation { get; }
        public bool IsValid => Generation != 0;
        public bool Equals(SimulationObjectHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is SimulationObjectHandle other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397) ^ (int)Generation);
        public override string ToString() => $"({Slot}, {Generation})";
        public static bool operator ==(SimulationObjectHandle left, SimulationObjectHandle right) => left.Equals(right);
        public static bool operator !=(SimulationObjectHandle left, SimulationObjectHandle right) => !left.Equals(right);
    }
}
