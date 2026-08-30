using System;

namespace Arena.Domain
{
    /// <summary>A finite point or direction in the arena plane, without an engine vector dependency.</summary>
    public readonly struct Position : IEquatable<Position>
    {
        public Position(float x, float y)
        {
            if (!IsFinite(x))
                throw new ArgumentOutOfRangeException(nameof(x), "Position must be finite.");
            if (!IsFinite(y))
                throw new ArgumentOutOfRangeException(nameof(y), "Position must be finite.");

            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool Equals(Position other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object value) => value is Position other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
    }
}
