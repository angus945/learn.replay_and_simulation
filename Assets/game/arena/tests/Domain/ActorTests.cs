using System;
using NUnit.Framework;

namespace Arena.Domain.Tests
{
    public sealed class ActorTests
    {
        [Test]
        public void IdentityUsesValueEqualityAndStableOrdering()
        {
            ActorId first = new ActorId(7);
            ActorId same = new ActorId(7);
            ActorId later = new ActorId(8);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first == same, Is.True);
            Assert.That(first.CompareTo(later), Is.LessThan(0));
            Assert.That(default(ActorId).IsValid, Is.False);
            Assert.That(first.IsValid, Is.True);
        }

        [Test]
        public void PositionRejectsNonFiniteCoordinates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Position(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Position(0f, float.PositiveInfinity));
        }

        [Test]
        public void ActorRejectsInvalidInitialState()
        {
            Assert.Throws<ArgumentException>(() => new Actor(default, ActorKind.Player, default, 4f, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Actor(new ActorId(1), (ActorKind)99, default, 4f, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Actor(new ActorId(1), ActorKind.Player, default, float.NaN, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Actor(new ActorId(1), ActorKind.Player, default, 4f, 0));
        }

        [Test]
        public void DiagonalMovementDoesNotExceedSpeed()
        {
            Actor actor = CreateActor();
            actor.SetDirection(1f, 1f);
            actor.Advance(0.5f);

            double distance = Math.Sqrt((double)actor.Position.X * actor.Position.X + (double)actor.Position.Y * actor.Position.Y);
            Assert.That(distance, Is.EqualTo(2d).Within(0.000001d));
        }

        [Test]
        public void AnalogueMovementRetainsSubUnitMagnitude()
        {
            Actor actor = CreateActor();
            actor.SetDirection(0.25f, 0f);
            actor.Advance(0.5f);

            Assert.That(actor.Position, Is.EqualTo(new Position(0.5f, 0f)));
        }

        [Test]
        public void LargeFiniteDirectionCanBeNormalizedWithoutOverflow()
        {
            Actor actor = CreateActor();
            actor.SetDirection(float.MaxValue, float.MaxValue);

            Assert.That(actor.Direction.X, Is.EqualTo(0.70710677f).Within(0.000001f));
            Assert.That(actor.Direction.Y, Is.EqualTo(actor.Direction.X));
        }

        [Test]
        public void DeathClampsHealthAndClearsMovement()
        {
            Actor actor = CreateActor();
            actor.SetDirection(1f, 0f);

            Assert.That(actor.TakeDamage(100), Is.EqualTo(30));
            Assert.That(actor.Health, Is.Zero);
            Assert.That(actor.IsDead, Is.True);
            Assert.That(actor.Direction, Is.EqualTo(new Position(0f, 0f)));
            Assert.That(actor.TakeDamage(10), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => actor.SetDirection(1f, 0f));

            actor.Advance(1f);
            Assert.That(actor.Position, Is.EqualTo(new Position(0f, 0f)));
        }

        [Test]
        public void InvalidDamageAndDirectionDoNotMutateActor()
        {
            Actor actor = CreateActor();
            actor.SetDirection(0.5f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => actor.TakeDamage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => actor.SetDirection(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => actor.Advance(-1f));
            Assert.That(actor.Health, Is.EqualTo(30));
            Assert.That(actor.Direction, Is.EqualTo(new Position(0.5f, 0f)));
        }

        [Test]
        public void MovementOverflowLeavesPositionUnchanged()
        {
            Position initial = new Position(float.MaxValue, 2f);
            Actor actor = new Actor(new ActorId(1), ActorKind.Player, initial, float.MaxValue, 30);
            actor.SetDirection(1f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => actor.Advance(2f));
            Assert.That(actor.Position, Is.EqualTo(initial));
        }

        [Test]
        public void RulesRejectInvalidRangesAndNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(speed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(attackRange: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(enemyHealthMin: 40, enemyHealthMax: 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(respawnMinTicks: 2, respawnMaxTicks: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(maxEnemySpawns: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaRules(damage: 0));
        }

        private static Actor CreateActor()
        {
            return new Actor(new ActorId(1), ActorKind.Player, new Position(0f, 0f), 4f, 30);
        }
    }
}
