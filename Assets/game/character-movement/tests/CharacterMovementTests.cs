using System;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using NUnit.Framework;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace CharacterMovement.Tests
{
    public sealed class CharacterMovementTests
    {
        [Test]
        public void DirectionLimitsDiagonalButPreservesAnalogMagnitude()
        {
            var diagonal = MovementDirection.FromAxes(1, 1);
            Assert.That(diagonal.X * diagonal.X + diagonal.Y * diagonal.Y, Is.EqualTo(1).Within(1e-6));
            Assert.That(MovementDirection.FromAxes(.25f, 0).X, Is.EqualTo(.25f));
            Assert.That(MovementDirection.FromAxes(float.MaxValue, float.MaxValue).X, Is.EqualTo(diagonal.X).Within(1e-6));
        }

        [Test]
        public void InvalidDomainValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MovementDirection.FromAxes(float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementPosition(0, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementAggregate(new CharacterId(1), default, -1));
            var character = new MovementAggregate(new CharacterId(1), default, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => character.Advance(-1));
        }

        [Test]
        public void AdvanceUsesSpeedAndElapsedTimeAndZeroDirectionStops()
        {
            var character = new MovementAggregate(new CharacterId(1), new MovementPosition(2, 3), 4);
            character.SetDesiredDirection(MovementDirection.FromAxes(1, 0));
            character.Advance(.25f);
            Assert.That(character.Position.X, Is.EqualTo(3));
            Assert.That(character.Position.Y, Is.EqualTo(3));
            character.SetDesiredDirection(default);
            character.Advance(1);
            Assert.That(character.Position.X, Is.EqualTo(3));
        }

        [Test]
        public void RepositoryUsesStableIdentityOrderAndRejectsDuplicates()
        {
            var repo = new CharacterMovementRepository();
            var second = new MovementAggregate(new CharacterId(2), default, 1);
            repo.Add(second);
            repo.Add(new MovementAggregate(new CharacterId(1), default, 1));
            Assert.That(repo.GetActiveOrdered()[0].Id.Value, Is.EqualTo(1));
            Assert.That(repo.GetActiveOrdered()[1], Is.SameAs(second));
            Assert.Throws<InvalidOperationException>(() => repo.Add(second));
        }

        [Test]
        public void UnknownCharacterIntentIsRejectedWithoutAffectingOthers()
        {
            var repo = new CharacterMovementRepository();
            var character = new MovementAggregate(new CharacterId(1), default, 1);
            repo.Add(character);
            var app = new MovementApplication(repo);
            Assert.That(app.TrySetDirection(new CharacterId(9), MovementDirection.FromAxes(1, 0)), Is.False);
            new PlayerMoveIntentHandler(app).Handle(new PlayerMoveIntent(new CharacterId(9), default));
            app.Advance(1);
            Assert.That(character.Position.X, Is.Zero);
        }

        [Test]
        public void PresenterInterpolatesFirstTickWithoutChangingDomain()
        {
            var repo = new CharacterMovementRepository();
            var character = new MovementAggregate(new CharacterId(1), default, 4);
            repo.Add(character);
            var view = new View();
            var presenter = new CharacterMovementPresenter(repo, character.Id, view);
            character.SetDesiredDirection(MovementDirection.FromAxes(1, 0));
            character.Advance(.25f);
            presenter.CaptureTickState(Context(1));
            presenter.Render(Context(1), .5f);
            Assert.That(view.Position.X, Is.EqualTo(.5f));
            Assert.That(character.Position.X, Is.EqualTo(1));
            presenter.Render(Context(1), -1);
            Assert.That(view.Position.X, Is.Zero);
            presenter.Render(Context(1), 2);
            Assert.That(view.Position.X, Is.EqualTo(1));
        }

        [Test]
        public void PresenterSnapsDiscontinuousTicksAndRejectsNaNAlpha()
        {
            var repo = new CharacterMovementRepository();
            var character = new MovementAggregate(new CharacterId(1), default, 4);
            repo.Add(character);
            var view = new View();
            var presenter = new CharacterMovementPresenter(repo, character.Id, view);
            character.SetDesiredDirection(MovementDirection.FromAxes(1, 0));
            character.Advance(1);
            presenter.CaptureTickState(Context(5));
            presenter.Render(Context(5), 0);
            Assert.That(view.Position.X, Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => presenter.Render(Context(5), float.NaN));
        }

        private static SimulationContext Context(ulong tick) =>
            new SimulationContext(new SimulationTick(tick, .25f), SimulationPhase.PresentationCapture);

        private sealed class View : ICharacterMovementView
        {
            public MovementPosition Position;
            public void SetPosition(MovementPosition position) => Position = position;
        }
    }
}
