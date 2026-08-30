using System;

namespace CharacterMovement.Domain
{
    /// <summary>Movement aggregate: no Unity object, simulation phase, repository or input-device dependency.</summary>
    public sealed class CharacterMovement
    {
        public CharacterMovement(CharacterId id, MovementPosition initialPosition, float speed)
        {
            if (!id.IsValid) throw new ArgumentException("Character identity must be valid.", nameof(id));
            MovementPosition.RequireFinite(speed, nameof(speed));
            if (speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed));
            Id = id;
            Position = initialPosition;
            Speed = speed;
        }

        public CharacterId Id { get; }
        public MovementPosition Position { get; private set; }
        public MovementDirection DesiredDirection { get; private set; }
        public float Speed { get; }

        public void SetDesiredDirection(MovementDirection direction) => DesiredDirection = direction;

        public void Advance(float elapsedSeconds)
        {
            MovementPosition.RequireFinite(elapsedSeconds, nameof(elapsedSeconds));
            if (elapsedSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            Position = new MovementPosition(
                Position.X + DesiredDirection.X * Speed * elapsedSeconds,
                Position.Y + DesiredDirection.Y * Speed * elapsedSeconds);
        }
    }
}
