using System;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using TickInputBuffering;

namespace CharacterMovement.Integration
{
    public sealed class PlayerMoveIntentHandler : IIntentHandler<PlayerMoveIntent>
    {
        private readonly MovementApplication application;
        public PlayerMoveIntentHandler(MovementApplication application)
        {
            this.application = application ?? throw new ArgumentNullException(nameof(application));
        }
        public void Handle(PlayerMoveIntent intent) =>
            application.TrySetDirection(intent.Character, intent.Direction);
    }

    public sealed class MovementPrePhysicsParticipant : IPrePhysicsParticipant
    {
        private readonly MovementApplication application;
        public MovementPrePhysicsParticipant(MovementApplication application)
        {
            this.application = application ?? throw new ArgumentNullException(nameof(application));
        }
        public void Tick(SimulationContext context) => application.Advance(context.Tick.DeltaTime);
    }

    public sealed class PlayerMovementInputSource : IIntentSource
    {
        private readonly ITickInputBuffer input;
        private readonly CharacterId character;
        private readonly int horizontalAxis;
        private readonly int verticalAxis;

        public PlayerMovementInputSource(ITickInputBuffer input, CharacterId character,
            int horizontalAxis, int verticalAxis)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            if (!character.IsValid) throw new ArgumentException("Invalid character.", nameof(character));
            this.character = character;
            this.horizontalAxis = horizontalAxis;
            this.verticalAxis = verticalAxis;
        }

        public void AcquireIntents(SimulationContext context, IIntentSink sink)
        {
            var frame = input.ConsumeTick(context.Tick.Number);
            var direction = MovementDirection.FromAxes(
                frame.GetAxis(horizontalAxis).Value, frame.GetAxis(verticalAxis).Value);
            sink.EnqueueIntent(new PlayerMoveIntent(character, direction));
        }
    }
}
