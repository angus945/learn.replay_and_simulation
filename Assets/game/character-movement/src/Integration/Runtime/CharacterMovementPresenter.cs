using System;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using DeterministicSimulation.Framework;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace CharacterMovement.Integration
{
    public sealed class CharacterMovementPresenter : IPresentationParticipant
    {
        private readonly ICharacterMovementRepository repository;
        private readonly CharacterId character;
        private readonly ICharacterMovementView view;
        private MovementPosition previous;
        private MovementPosition current;
        private ulong capturedTick;

        public CharacterMovementPresenter(ICharacterMovementRepository repository,
            CharacterId character, ICharacterMovementView view)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.character = character;
            current = ReadPosition();
            previous = current; // Seed tick zero; first movement tick interpolates from the spawn pose.
            view.SetPosition(current);
        }

        public void CaptureTickState(SimulationContext context)
        {
            MovementPosition next = ReadPosition();
            bool continuous = capturedTick != ulong.MaxValue && context.Tick.Number == capturedTick + 1;
            previous = continuous ? current : next;
            current = next;
            capturedTick = context.Tick.Number;
        }

        public void Render(SimulationContext context, float interpolationAlpha)
        {
            if (float.IsNaN(interpolationAlpha) || float.IsInfinity(interpolationAlpha))
                throw new ArgumentOutOfRangeException(nameof(interpolationAlpha));
            float alpha = Math.Max(0f, Math.Min(1f, interpolationAlpha));
            view.SetPosition(new MovementPosition(
                previous.X + (current.X - previous.X) * alpha,
                previous.Y + (current.Y - previous.Y) * alpha));
        }

        private MovementPosition ReadPosition()
        {
            if (!repository.TryGet(character, out MovementAggregate aggregate))
                throw new InvalidOperationException("The presented character no longer exists.");
            return aggregate.Position;
        }
    }
}
