using System;

namespace CharacterMovement.Domain
{
    public readonly struct CharacterId : IEquatable<CharacterId>, IComparable<CharacterId>
    {
        public CharacterId(ulong value)
        {
            if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(CharacterId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CharacterId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(CharacterId other) => Value.CompareTo(other.Value);
    }

    public readonly struct MovementPosition
    {
        public MovementPosition(float x, float y)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            X = x;
            Y = y;
        }
        public float X { get; }
        public float Y { get; }
        internal static void RequireFinite(float value, string parameter)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

    /// <summary>Analog input inside a unit disk; diagonal input never increases maximum speed.</summary>
    public readonly struct MovementDirection
    {
        private MovementDirection(float x, float y) { X = x; Y = y; }
        public float X { get; }
        public float Y { get; }

        public static MovementDirection FromAxes(float horizontal, float vertical)
        {
            MovementPosition.RequireFinite(horizontal, nameof(horizontal));
            MovementPosition.RequireFinite(vertical, nameof(vertical));
            double magnitude = Math.Sqrt((double)horizontal * horizontal + (double)vertical * vertical);
            if (magnitude > 1d)
                return new MovementDirection((float)(horizontal / magnitude), (float)(vertical / magnitude));
            return new MovementDirection(horizontal, vertical);
        }
    }
}
