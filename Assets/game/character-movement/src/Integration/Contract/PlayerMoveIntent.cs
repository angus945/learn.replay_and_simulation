using CharacterMovement.Domain;
using DeterministicSimulation;

namespace CharacterMovement.Integration
{
    /// <summary>External movement request. No GameObject, pool slot or framework runtime is embedded.</summary>
    public readonly struct PlayerMoveIntent : IIntent
    {
        public PlayerMoveIntent(CharacterId character, MovementDirection direction)
        {
            Character = character;
            Direction = direction;
        }
        public CharacterId Character { get; }
        public MovementDirection Direction { get; }
    }
}
