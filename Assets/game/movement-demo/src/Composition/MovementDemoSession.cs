using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation.Framework;
using TickInput;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace MovementDemo
{
    /// <summary>Project composition root; one character, no physics or structural changes.</summary>
    public sealed class MovementDemoSession
    {
        private readonly TickInputBuffer input = new TickInputBuffer();
        private readonly MovementAggregate character;
        private readonly SimulationRunner runner;

        public MovementDemoSession(ICharacterMovementView view, float speed = 4f, float tickDeltaTime = 1f / 60f)
        {
            character = new MovementAggregate(new CharacterId(1), default, speed);
            var repository = new CharacterMovementRepository();
            repository.Add(character);
            var application = new MovementApplication(repository);
            input.RegisterAxis(0);
            input.RegisterAxis(1);
            input.Seal();
            var pipeline = new SimulationPipeline();
            pipeline.RegisterIntentSource(new PlayerMovementInputSource(input, character.Id, 0, 1));
            pipeline.RegisterIntentHandler(new PlayerMoveIntentHandler(application));
            pipeline.RegisterPrePhysicsParticipant(new MovementPrePhysicsParticipant(application));
            pipeline.RegisterPresentationParticipant(new CharacterMovementPresenter(repository, character.Id, view));
            pipeline.Seal();
            runner = new SimulationRunner(pipeline, tickDeltaTime);
        }

        public MovementPosition CurrentPosition => character.Position;
        public ulong TickNumber => runner.TickNumber;
        public float PresentationAlpha => runner.PresentationAlpha;

        public void CaptureAxes(float horizontal, float vertical)
        {
            // Validate both before modifying either axis.
            MovementDirection.FromAxes(horizontal, vertical);
            input.CaptureAxis(0, horizontal);
            input.CaptureAxis(1, vertical);
        }

        public void AdvanceTime(float seconds) => runner.AdvanceTime(seconds);
        public void UpdatePresentation() => runner.UpdatePresentation();
    }
}
