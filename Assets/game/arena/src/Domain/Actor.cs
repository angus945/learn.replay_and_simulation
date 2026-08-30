using System;

namespace Arena.Domain
{
    public enum ActorKind
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>The aggregate root protects health, movement and death consistency.</summary>
    public sealed class Actor
    {
        public Actor(ActorId id, ActorKind kind, Position position, float speed, int maxHealth)
        {
            if (!id.IsValid)
                throw new ArgumentException("An actor requires a nonzero identity.", nameof(id));
            if (kind != ActorKind.Player && kind != ActorKind.Enemy)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Arena.Domain.Position.IsFinite(speed) || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));

            Id = id;
            Kind = kind;
            Position = position;
            Speed = speed;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Direction = new Position(0f, 0f);
        }

        public ActorId Id { get; }
        public ActorKind Kind { get; }
        public Position Position { get; private set; }
        public Position Direction { get; private set; }
        public float Speed { get; }
        public int Health { get; private set; }
        public int MaxHealth { get; }
        public bool IsDead => Health == 0;

        public void SetDirection(float x, float y)
        {
            if (!Arena.Domain.Position.IsFinite(x) || !Arena.Domain.Position.IsFinite(y))
                throw new ArgumentOutOfRangeException(nameof(x), "Direction must be finite.");
            if (IsDead)
                throw new InvalidOperationException("A defeated actor cannot move.");

            // Preserve analogue magnitude, but diagonal input cannot increase movement speed.
            double lengthSquared = (double)x * x + (double)y * y;
            if (lengthSquared > 1d)
            {
                double inverseLength = 1d / Math.Sqrt(lengthSquared);
                x = (float)(x * inverseLength);
                y = (float)(y * inverseLength);
            }

            Direction = new Position(x, y);
        }

        public void Advance(float seconds)
        {
            if (!Arena.Domain.Position.IsFinite(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (IsDead)
                return;

            // Build the next value first: validation failure cannot partially change the aggregate.
            float nextX = (float)(Position.X + (double)Direction.X * Speed * seconds);
            float nextY = (float)(Position.Y + (double)Direction.Y * Speed * seconds);
            Position next = new Position(nextX, nextY);
            Position = next;
        }

        public int TakeDamage(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            int applied = Math.Min(Health, amount);
            Health -= applied;
            if (IsDead)
                Direction = new Position(0f, 0f);
            return applied;
        }
    }
}
