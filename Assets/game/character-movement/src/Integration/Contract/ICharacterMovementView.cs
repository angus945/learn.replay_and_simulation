using CharacterMovement.Domain;

namespace CharacterMovement.Integration
{
    /// <summary>Presentation-only output. Implementations must not feed interpolated positions back into Domain.</summary>
    public interface ICharacterMovementView
    {
        void SetPosition(MovementPosition position);
    }
}
