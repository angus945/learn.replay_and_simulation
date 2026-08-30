using System;
using System.Collections.Generic;
using Arena.Domain;
using NUnit.Framework;

namespace Arena.Application.Tests
{
    public sealed class ArenaApplicationTests
    {
        [Test]
        public void NewSessionInitializesAndCommitsPlayerAndOneEnemy()
        {
            Fixture fixture = new Fixture();

            Assert.That(fixture.App.PlayerId, Is.EqualTo(new ActorId(1)));
            Assert.That(fixture.App.LastActorId, Is.EqualTo(2ul));
            Assert.That(fixture.App.EnemiesSpawned, Is.EqualTo(1));
            Assert.That(fixture.App.Actors.Count, Is.EqualTo(2));
            Assert.That(fixture.App.Actors[1].Position, Is.EqualTo(new Position(1f, 0f)));
            Assert.That(fixture.Random.HealthDraws, Is.EqualTo(1));
            Assert.That(fixture.Random.DelayDraws, Is.Zero);
            Assert.That(fixture.Lifecycle.Commits, Is.EqualTo(1));
        }

        [Test]
        public void MoveChangesIntentThenAdvanceChangesPosition()
        {
            Fixture fixture = new Fixture();
            ArenaResult result = fixture.App.Execute(new ArenaRequest(ArenaAction.Move, fixture.App.PlayerId, x: 1f));

            Assert.That(result.Decision, Is.EqualTo(ArenaDecision.Accepted));
            Assert.That(fixture.App.Actors[0].Position.X, Is.Zero);
            fixture.App.Advance(1, 0.25f);
            Assert.That(fixture.App.Actors[0].Position.X, Is.EqualTo(1f));
            Assert.That(fixture.App.Tick, Is.EqualTo(1ul));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.App.Advance(1, 0.25f));
        }

        [Test]
        public void InvalidInputDoesNotDrawRandomOrChangeActor()
        {
            Fixture fixture = new Fixture();
            ArenaResult result = fixture.App.Execute(new ArenaRequest(ArenaAction.Move, fixture.App.PlayerId, x: float.NaN));

            Assert.That(result.Decision, Is.EqualTo(ArenaDecision.InvalidRequest));
            Assert.That(result.Code, Is.EqualTo("invalid-direction"));
            Assert.That(fixture.App.Actors[0].Direction, Is.EqualTo(new Position(0f, 0f)));
            Assert.That(fixture.Random.HealthDraws, Is.EqualTo(1));
            Assert.That(fixture.Random.DelayDraws, Is.Zero);
        }

        [Test]
        public void OutOfRangeAttackIsRejectedWithoutDamage()
        {
            Fixture fixture = new Fixture();
            fixture.App.Execute(new ArenaRequest(ArenaAction.Move, fixture.App.PlayerId, x: -1f));
            fixture.App.Advance(1, 1f);

            ArenaResult result = fixture.App.Execute(new ArenaRequest(ArenaAction.Attack, fixture.App.PlayerId, new ActorId(2)));

            Assert.That(result.Code, Is.EqualTo("out-of-range"));
            Assert.That(result.Decision, Is.EqualTo(ArenaDecision.Rejected));
            Assert.That(fixture.App.Actors[1].Health, Is.EqualTo(20));
            Assert.That(result.Facts.Count, Is.Zero);
        }

        [Test]
        public void DefeatProducesFactsButDoesNotInvokeOuterReactions()
        {
            Fixture fixture = new Fixture(new ArenaRules(damage: 100));
            ArenaResult result = fixture.App.Execute(new ArenaRequest(ArenaAction.Attack, fixture.App.PlayerId, new ActorId(2)));

            Assert.That(result.Code, Is.EqualTo("defeated"));
            Assert.That(result.Facts.Count, Is.EqualTo(2));
            Assert.That(result.Facts[0].Kind, Is.EqualTo(ArenaFactKind.Damaged));
            Assert.That(result.Facts[0].Amount, Is.EqualTo(20));
            Assert.That(result.Facts[1].Kind, Is.EqualTo(ArenaFactKind.Defeated));
            Assert.That(result.Facts[1].Actor, Is.EqualTo(fixture.App.PlayerId));
            Assert.That(result.Facts[1].Target, Is.EqualTo(new ActorId(2)));
            Assert.That(fixture.App.Actors.Count, Is.EqualTo(2));
            Assert.That(fixture.Lifecycle.PendingRemovals, Is.Zero);
            Assert.That(fixture.App.PendingRespawnTicks.Count, Is.Zero);
        }

        [Test]
        public void DefeatedActorCannotMoveOrTakeAnotherAttack()
        {
            Fixture fixture = new Fixture(new ArenaRules(damage: 100));
            fixture.App.Execute(new ArenaRequest(ArenaAction.Attack, fixture.App.PlayerId, new ActorId(2)));

            ArenaResult move = fixture.App.Execute(new ArenaRequest(ArenaAction.Move, new ActorId(2), x: 1f));
            ArenaResult repeatAttack = fixture.App.Execute(new ArenaRequest(ArenaAction.Attack, fixture.App.PlayerId, new ActorId(2)));

            Assert.That(move.Code, Is.EqualTo("actor-dead"));
            Assert.That(repeatAttack.Code, Is.EqualTo("target-dead"));
            Assert.That(repeatAttack.Facts.Count, Is.Zero);
        }

        [Test]
        public void DefeatReactionStagesRemovalUntilStructuralCommit()
        {
            Fixture fixture = new Fixture(new ArenaRules(damage: 100));
            fixture.App.Execute(new ArenaRequest(ArenaAction.Attack, fixture.App.PlayerId, new ActorId(2)));
            fixture.App.OnDefeated(new ActorId(2));
            fixture.App.OnDefeated(new ActorId(2));

            Assert.That(fixture.App.Actors.Count, Is.EqualTo(2));
            Assert.That(fixture.Lifecycle.PendingRemovals, Is.EqualTo(1));
            fixture.App.Commit(1);
            Assert.That(fixture.App.Actors.Count, Is.EqualTo(1));
        }

        [Test]
        public void LivingActorsCannotBeRemovedByDefeatReaction()
        {
            Fixture fixture = new Fixture();
            fixture.App.OnDefeated(fixture.App.PlayerId);
            fixture.App.OnDefeated(new ActorId(99));
            fixture.App.Commit(1);

            Assert.That(fixture.App.Actors.Count, Is.EqualTo(2));
        }

        [Test]
        public void SpawnBudgetIncludesReservationsAndRejectedReservationConsumesNoRandom()
        {
            Fixture fixture = new Fixture(new ArenaRules(maxEnemySpawns: 2, respawnMinTicks: 3, respawnMaxTicks: 3));

            Assert.That(fixture.App.ScheduleRespawn(1), Is.True);
            Assert.That(fixture.App.ScheduleRespawn(1), Is.False);
            Assert.That(fixture.Random.DelayDraws, Is.EqualTo(1));
            Assert.That(fixture.Random.HealthDraws, Is.EqualTo(1));
            fixture.App.Commit(3);
            Assert.That(fixture.App.EnemiesSpawned, Is.EqualTo(1));
            fixture.App.Commit(4);
            Assert.That(fixture.App.EnemiesSpawned, Is.EqualTo(2));
            Assert.That(fixture.App.LastActorId, Is.EqualTo(3ul));
            Assert.That(fixture.App.ScheduleRespawn(4), Is.False);
            Assert.That(fixture.Random.DelayDraws, Is.EqualTo(1));
            Assert.That(fixture.Random.HealthDraws, Is.EqualTo(2));
        }

        [Test]
        public void PendingScheduleIsSortedDetachedAndCanContainEqualDueTicks()
        {
            Fixture fixture = new Fixture(new ArenaRules(respawnMinTicks: 2, respawnMaxTicks: 2));
            fixture.App.ScheduleRespawn(10);
            fixture.App.ScheduleRespawn(1);
            fixture.App.ScheduleRespawn(1);
            IReadOnlyList<ulong> before = fixture.App.PendingRespawnTicks;

            Assert.That(before, Is.EqualTo(new ulong[] { 3, 3, 12 }));
            fixture.App.Commit(3);
            Assert.That(fixture.App.PendingRespawnTicks, Is.EqualTo(new ulong[] { 12 }));
            Assert.That(before, Is.EqualTo(new ulong[] { 3, 3, 12 }));
        }

        [Test]
        public void ZeroDelayReservationSpawnsAtSameTickCommit()
        {
            Fixture fixture = new Fixture(new ArenaRules(respawnMinTicks: 0, respawnMaxTicks: 0));
            fixture.App.ScheduleRespawn(1);
            fixture.App.Commit(1);

            Assert.That(fixture.App.EnemiesSpawned, Is.EqualTo(2));
            Assert.That(fixture.App.PendingRespawnTicks.Count, Is.Zero);
        }

        [Test]
        public void DueTickOverflowIsRejectedBeforeDrawingRandom()
        {
            Fixture fixture = new Fixture();

            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.App.ScheduleRespawn(ulong.MaxValue));
            Assert.That(fixture.Random.DelayDraws, Is.Zero);
            Assert.That(fixture.App.PendingRespawnTicks.Count, Is.Zero);
        }

        [Test]
        public void ResultCopiesFactsInsteadOfLeakingMutableArray()
        {
            ArenaFact fact = new ArenaFact(ArenaFactKind.Damaged, new ActorId(1), new ActorId(2), 10);
            ArenaFact[] source = { fact };
            ArenaResult result = new ArenaResult(ArenaDecision.Accepted, "damaged", source);
            source[0] = default;

            Assert.That(result.Facts[0].Amount, Is.EqualTo(10));
            Assert.That(result.Facts[0].Target, Is.EqualTo(new ActorId(2)));
        }

        private sealed class Fixture
        {
            public Fixture(ArenaRules rules = null)
            {
                Repository repository = new Repository();
                Lifecycle = new Lifecycle(repository);
                Random = new RandomStub();
                App = new ArenaApplication(repository, Lifecycle, Random, rules ?? new ArenaRules());
            }

            public ArenaApplication App { get; }
            public Lifecycle Lifecycle { get; }
            public RandomStub Random { get; }
        }

        private sealed class Repository : IActorRepository
        {
            private readonly SortedDictionary<ActorId, Actor> actors = new SortedDictionary<ActorId, Actor>();

            public void Add(Actor actor) => actors.Add(actor.Id, actor);
            public bool Remove(ActorId id) => actors.Remove(id);
            public bool TryGet(ActorId id, out Actor actor) => actors.TryGetValue(id, out actor);
            public IReadOnlyList<Actor> ReadOrdered() => new List<Actor>(actors.Values).AsReadOnly();
        }

        private sealed class Lifecycle : IActorLifecycle
        {
            private readonly IActorRepository repository;
            private readonly HashSet<ActorId> removals = new HashSet<ActorId>();

            public Lifecycle(IActorRepository repository)
            {
                this.repository = repository;
            }

            public int Commits { get; private set; }
            public int PendingRemovals => removals.Count;
            public void Spawn(Actor actor) => repository.Add(actor);
            public void Despawn(ActorId id) => removals.Add(id);
            public bool IsActive(ActorId id) => repository.TryGet(id, out Actor actor) && !removals.Contains(actor.Id);

            public void Commit()
            {
                foreach (ActorId id in removals)
                    repository.Remove(id);
                removals.Clear();
                Commits++;
            }
        }

        private sealed class RandomStub : ISpawnRandom
        {
            public int HealthDraws { get; private set; }
            public int DelayDraws { get; private set; }

            public int NextHealth(int min, int maxInclusive)
            {
                HealthDraws++;
                return min;
            }

            public int NextDelay(int min, int maxInclusive)
            {
                DelayDraws++;
                return min;
            }
        }
    }
}
