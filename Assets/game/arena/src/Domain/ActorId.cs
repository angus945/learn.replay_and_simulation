using System;

namespace Arena.Domain
{
    /// <summary>A game identity. It is deliberately unrelated to a framework registry handle.</summary>
    public readonly struct ActorId : IEquatable<ActorId>, IComparable<ActorId>
    {
        public ActorId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public bool Equals(ActorId other) => Value == other.Value;
        public override bool Equals(object value) => value is ActorId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ActorId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);
        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }
}
