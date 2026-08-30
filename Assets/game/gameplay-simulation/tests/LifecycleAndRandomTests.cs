using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Testability;

namespace GameplaySimulation.Tests
{
    public sealed class LifecycleAndRandomTests
    {
        private static GameplaySession New(GameplayScenario scenario)
        { GameplaySession session = new GameplaySession(); session.Start(scenario); return session; }
        private static GameplayRequest Attack(GameplaySession s, ulong sequence, ulong tick, ulong target)
            => new GameplayRequest(s.Id, sequence, tick, GameplayActionKind.Attack, 1, target);

        [Test]
        public void RespawnCommitsAfterCommandsAndOldIdNeverTargetsReplacement()
        {
            GameplaySession s = New(new GameplayScenario(health: 10, respawnEnemies: true));
            s.Submit(Attack(s, 1, 1, 2)); s.Submit(Attack(s, 2, 1, 2));
            s.Submit(new GameplayRequest(s.Id, 3, 1, GameplayActionKind.Move, 3, x: 1));
            TickReport first = s.Step();
            Assert.That(first.Results.Select(r => r.Code), Is.EqualTo(new[] { "attack.applied", "target.dead", "actor.unknown" }));
            Assert.That(s.Observe().Actors[1].Active, Is.False);
            Assert.That(s.Observe().Actors[2].Id, Is.EqualTo(3));
            Assert.That(s.Observe().Actors[2].X, Is.EqualTo(1));
            s.Submit(new GameplayRequest(s.Id, 4, 2, GameplayActionKind.Move, 3, x: 1));
            s.Submit(Attack(s, 5, 2, 2)); s.Step();
            Assert.That(s.Observe().Actors[2].X, Is.GreaterThan(1));
            Assert.That(s.Observe().Actors[2].Health, Is.EqualTo(10));
            LifecycleSnapshot snapshot = s.ObserveLifecycle();
            Assert.That(snapshot.Active, Is.EqualTo(2)); Assert.That(snapshot.RepositoryCount, Is.EqualTo(2));
            Assert.That(snapshot.PendingSpawns, Is.Zero);
        }
        [TestCase(1f / 30)]
        [TestCase(1f / 144)]
        [TestCase(.37f)]
        public void DelayedRespawnIsTickBoundedIndependentAndReplayable(float frameDelta)
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true,
                enemyHealthMin: 20, enemyHealthMax: 40, randomRespawnDelay: true);
            GameplaySession s = New(scenario);
            ulong healthState = s.Observe().EnemyRandomState;
            ulong delayState = s.Observe().RespawnRandomState;
            s.Submit(Attack(s, 1, 1, 2)); s.Submit(Attack(s, 2, 1, 2)); s.Step();
            GameplayObservation waiting = s.Observe();
            Assert.That(waiting.PendingRespawnTicks.Count, Is.EqualTo(1));
            ulong due = waiting.PendingRespawnTicks[0];
            Assert.That((due - 1) * (double)scenario.TickDelta, Is.InRange(1d, 3d));
            Assert.That(waiting.EnemyRandomState, Is.EqualTo(healthState));
            Assert.That(waiting.RespawnRandomState, Is.Not.EqualTo(delayState));
            while (s.CurrentTick + 1 < due)
            {
                s.Step();
                Assert.That(s.ObserveLifecycle().Active, Is.EqualTo(1));
                Assert.That(s.Observe().RespawnRandomState, Is.EqualTo(waiting.RespawnRandomState));
            }
            s.Step();
            Assert.That(s.ObserveLifecycle().Active, Is.EqualTo(2));
            Assert.That(s.ObserveLifecycle().PendingSpawns, Is.Zero);
            Assert.That(waiting.PendingRespawnTicks.Count, Is.EqualTo(1)); // Immutable snapshot.
            using (MemoryStream stream = new MemoryStream())
            {
                ArtifactJson.Write(stream, s.CaptureReplay()); stream.Position = 0;
                ReplayPlayback replay = new ReplayPlayback(ArtifactJson.Read<ReplayArtifact>(stream)); replay.Play();
                for (int i = 0; i < 10000 && replay.State == ReplayPlaybackState.Playing; i++) replay.AdvanceTime(frameDelta);
                Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
                Assert.That(replay.FirstDifference, Is.Null);
            }
            s.Reset(scenario);
            Assert.That(s.Observe().PendingRespawnTicks, Is.Empty);
            Assert.That(s.Observe().RespawnRandomState, Is.EqualTo(delayState));
            s.Submit(Attack(s, 1, 1, 2)); s.Step();
            Assert.That(s.Observe().PendingRespawnTicks[0], Is.EqualTo(due));
        }

        [Test]
        public void RespawnDelayVariesBySeedAndBudgetDoesNotConsumeRandom()
        {
            ulong[] delays = Enumerable.Range(1, 16).Select(seed =>
            {
                GameplaySession s = New(new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true,
                    randomRespawnDelay: true, seed: (ulong)seed));
                s.Submit(Attack(s, 1, 1, 2)); s.Step();
                return s.Observe().PendingRespawnTicks[0];
            }).ToArray();
            Assert.That(delays.Distinct().Count(), Is.GreaterThan(1));
            GameplaySession capped = New(new GameplayScenario(damage: 100, respawnEnemies: true, maxEnemySpawns: 1, randomRespawnDelay: true));
            ulong initial = capped.Observe().RespawnRandomState;
            capped.Submit(Attack(capped, 1, 1, 2)); capped.Step();
            Assert.That(capped.Observe().PendingRespawnTicks, Is.Empty);
            Assert.That(capped.Observe().RespawnRandomState, Is.EqualTo(initial));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(randomRespawnDelay: true));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(respawnEnemies: true, randomRespawnDelay: true, tickDelta: 4));
        }
        [Test]
        public void SpawnBudgetBoundsTombstonesAndDoesNotFaultSession()
        {
            GameplaySession s = New(new GameplayScenario(health: 10, respawnEnemies: true, maxEnemySpawns: 3));
            for (ulong i = 1; i <= 3; i++) { s.Submit(Attack(s, i, i, i + 1)); s.Step(); }
            Assert.That(s.State, Is.EqualTo(SessionState.Running));
            Assert.That(s.ObserveLifecycle().RetainedActors, Is.EqualTo(4));
            Assert.That(s.ObserveLifecycle().Active, Is.EqualTo(1));
            Assert.That(s.ObserveLifecycle().RepositoryCount, Is.EqualTo(1));
            Assert.That(s.ReadTrace().Any(t => t.Code == "spawn.budget"), Is.True);
        }
        [Test]
        public void RandomIsConsumedOnlyOnEnemyBirthAndResetRestoresIt()
        {
            GameplayScenario scenario = new GameplayScenario(respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40, seed: 42);
            GameplaySession s = New(scenario);
            ulong initialState = s.Observe().EnemyRandomState;
            int initialHealth = s.Observe().Actors[1].Health;
            string initialHash = s.HashHistory[0].Hash;
            s.Submit(Attack(s, 1, 1, 999)); s.Step();
            Assert.That(s.Observe().EnemyRandomState, Is.EqualTo(initialState));
            Assert.That(s.Observe().Actors[0].Health, Is.EqualTo(30));
            Assert.That(initialHealth, Is.InRange(20, 40));
            s.Reset(scenario);
            Assert.That(s.Observe().EnemyRandomState, Is.EqualTo(initialState));
            Assert.That(s.Observe().Actors[1].Health, Is.EqualTo(initialHealth));
            Assert.That(s.HashHistory[0].Hash, Is.EqualTo(initialHash));
        }
        [TestCase(1f / 30)]
        [TestCase(1f / 60)]
        [TestCase(1f / 144)]
        [TestCase(.37f)]
        public void RepeatedRandomRespawnsReplayAcrossFrameSchedules(float frameDelta)
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true,
                enemyHealthMin: 20, enemyHealthMax: 40, maxEnemySpawns: 10, seed: 77);
            GameplaySession first = New(scenario), reversed = New(scenario);
            for (ulong tick = 1; tick <= 8; tick++) first.Submit(Attack(first, tick, tick, tick + 1));
            for (ulong tick = 8; tick > 0; tick--) reversed.Submit(Attack(reversed, tick, tick, tick + 1));
            for (int tick = 0; tick < 12; tick++) Assert.That(first.Step().StateHash, Is.EqualTo(reversed.Step().StateHash));
            Assert.That(first.ObserveLifecycle().EnemiesSpawned, Is.EqualTo(9));
            Assert.That(first.Observe().Actors.Skip(1).Select(a => a.MaxHealth).Distinct().Count(), Is.GreaterThan(1));
            using (MemoryStream stream = new MemoryStream())
            {
                ArtifactJson.Write(stream, first.CaptureReplay()); stream.Position = 0;
                ReplayPlayback replay = new ReplayPlayback(ArtifactJson.Read<ReplayArtifact>(stream)); replay.Play();
                for (int i = 0; i < 10000 && replay.State == ReplayPlaybackState.Playing; i++) replay.AdvanceTime(frameDelta);
                Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
                Assert.That(replay.FirstDifference, Is.Null);
                Assert.That(replay.Observe().EnemyRandomState, Is.EqualTo(first.Observe().EnemyRandomState));
            }
        }
        [Test]
        public void DifferentSeedsProduceDifferentHealthSequences()
        {
            int[] values = Enumerable.Range(1, 16).Select(seed => New(new GameplayScenario(enemyHealthMin: 20, enemyHealthMax: 40, seed: (ulong)seed)).Observe().Actors[1].Health).ToArray();
            Assert.That(values.Distinct().Count(), Is.GreaterThan(1));
        }
        [Test]
        public void FailurePreservesFirstEvidenceStageAndCancelsFutureInputs()
        {
            GameplaySession s = New(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            s.Step();
            s.Submit(new GameplayRequest(s.Id, 1, 2, GameplayActionKind.Move, 1, x: 1));
            s.Submit(new GameplayRequest(s.Id, 2, 3, GameplayActionKind.Move, 1)); s.Step();
            FailureArtifact saved = s.Failure;
            Assert.That(saved.FailureStage, Is.EqualTo("PrePhysics"));
            Assert.That(saved.LastCompletedTick, Is.EqualTo(1));
            Assert.That(saved.ActionSequence, Is.Zero);
            Assert.That(s.Results.Find(s.Id, 2).CancellationReason, Is.EqualTo("session.faulted"));
            Assert.Throws<InvalidOperationException>(() => s.Step()); s.Stop();
            Assert.That(s.Failure, Is.SameAs(saved));
            Assert.That(ScenarioRerun.VerifyFailure(saved), Is.True);
        }
        [Test]
        public void StopCancellationReasonIsDistinctFromFault()
        {
            GameplaySession s = New(new GameplayScenario());
            s.Submit(Attack(s, 1, 5, 2)); s.Stop();
            Assert.That(s.Results.Find(s.Id, 1).CancellationReason, Is.EqualTo("session.stopped"));
        }
        [Test]
        public void InvalidRandomRangeAndSpawnBudgetFailBeforeWorldReplacement()
        {
            Assert.Throws<ArgumentException>(() => new GameplayScenario(enemyHealthMin: 40, enemyHealthMax: 20));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(respawnEnemies: true, maxEnemySpawns: 0));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(enemyHealthMin: 1, enemyHealthMax: int.MaxValue));
        }
    }
}
