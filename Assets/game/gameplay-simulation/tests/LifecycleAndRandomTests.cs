using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplaySimulation.Tests
{
    public sealed class LifecycleAndRandomTests
    {
        [Test]
        public void RespawnCommitsAfterCommandsAndOldIdNeverTargetsReplacement()
        {
            using ModernSession s = new GameplayDefinition().CreateTestSession(new GameplayScenario(health: 10, respawnEnemies: true));
            s.Gameplay.Submit(s.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Gameplay.Submit(s.Id, 2, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            s.Gameplay.Submit(s.Id, 3, 1, new GameplayInput(GameplayActionKind.Move, 3, x: 1));
            TemplateTick first = s.Simulation.Step();
            Assert.That(first.Results.Select(r => r.Code), Is.EqualTo(new[] { "attack.applied", "target.dead", "actor.unknown" }));
            Assert.That(s.Observe().Actors[1].Active, Is.False);
            Assert.That(s.Observe().Actors[2].Id, Is.EqualTo(3));
            Assert.That(s.Observe().Actors[2].X, Is.EqualTo(1));
            s.Gameplay.Submit(s.Id, 4, 2, new GameplayInput(GameplayActionKind.Move, 3, x: 1));
            s.Gameplay.Submit(s.Id, 5, 2, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Simulation.Step();
            Assert.That(s.Observe().Actors[2].X, Is.GreaterThan(1));
            Assert.That(s.Observe().Actors[2].Health, Is.EqualTo(10));
            LifecycleSnapshot snapshot = s.Observe().Lifecycle;
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
            using ModernSession s = new GameplayDefinition().CreateTestSession(scenario);
            ulong healthState = s.Observe().EnemyRandomState;
            ulong delayState = s.Observe().RespawnRandomState;
            s.Gameplay.Submit(s.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Gameplay.Submit(s.Id, 2, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Simulation.Step();
            GameplayObservation waiting = s.Observe();
            Assert.That(waiting.PendingRespawnTicks.Count, Is.EqualTo(1));
            ulong due = waiting.PendingRespawnTicks[0];
            Assert.That((due - 1) * (double)scenario.TickDelta, Is.InRange(1d, 3d));
            Assert.That(waiting.EnemyRandomState, Is.EqualTo(healthState));
            Assert.That(waiting.RespawnRandomState, Is.Not.EqualTo(delayState));
            while (s.CurrentTick + 1 < due)
            {
                s.Simulation.Step();
                Assert.That(s.Observe().Lifecycle.Active, Is.EqualTo(1));
                Assert.That(s.Observe().RespawnRandomState, Is.EqualTo(waiting.RespawnRandomState));
            }
            s.Simulation.Step();
            Assert.That(s.Observe().Lifecycle.Active, Is.EqualTo(2));
            Assert.That(s.Observe().Lifecycle.PendingSpawns, Is.Zero);
            Assert.That(waiting.PendingRespawnTicks.Count, Is.EqualTo(1)); // Immutable snapshot.
            using (MemoryStream stream = new MemoryStream())
            {
                TemplateRecordingIO.Write(stream, s.CaptureRecording()); stream.Position = 0;
                using ModernReplay replay = new GameplayDefinition().CreateReplay(TemplateRecordingIO.Read(stream)); replay.Play();
                for (int i = 0; i < 10000 && replay.State == TemplateReplayState.Playing; i++) replay.AdvanceTime(frameDelta);
                Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
                Assert.That(replay.FirstDifference, Is.Null);
            }
            s.Admin.Reset(scenario);
            Assert.That(s.Observe().PendingRespawnTicks, Is.Empty);
            Assert.That(s.Observe().RespawnRandomState, Is.EqualTo(delayState));
            s.Gameplay.Submit(s.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Simulation.Step();
            Assert.That(s.Observe().PendingRespawnTicks[0], Is.EqualTo(due));
        }

        [Test]
        public void RespawnDelayVariesBySeedAndBudgetDoesNotConsumeRandom()
        {
            ulong[] delays = Enumerable.Range(1, 16).Select(seed =>
            {
                using ModernSession s = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true,
                    randomRespawnDelay: true, seed: (ulong)seed));
                s.Gameplay.Submit(s.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Simulation.Step();
                return s.Observe().PendingRespawnTicks[0];
            }).ToArray();
            Assert.That(delays.Distinct().Count(), Is.GreaterThan(1));
            using ModernSession capped = new GameplayDefinition().CreateTestSession(new GameplayScenario(damage: 100, respawnEnemies: true, maxEnemySpawns: 1, randomRespawnDelay: true));
            ulong initial = capped.Observe().RespawnRandomState;
            capped.Gameplay.Submit(capped.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)); capped.Simulation.Step();
            Assert.That(capped.Observe().PendingRespawnTicks, Is.Empty);
            Assert.That(capped.Observe().RespawnRandomState, Is.EqualTo(initial));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(randomRespawnDelay: true));
            Assert.Throws<ArgumentException>(() => new GameplayScenario(respawnEnemies: true, randomRespawnDelay: true, tickDelta: 4));
        }
        [Test]
        public void SpawnBudgetBoundsTombstonesAndDoesNotFaultSession()
        {
            using ModernSession s = new GameplayDefinition().CreateTestSession(new GameplayScenario(health: 10, respawnEnemies: true, maxEnemySpawns: 3));
            for (ulong i = 1; i <= 3; i++) { s.Gameplay.Submit(s.Id, i, i, new GameplayInput(GameplayActionKind.Attack, 1, i + 1)); s.Simulation.Step(); }
            Assert.That(s.State, Is.EqualTo(SessionState.Running));
            Assert.That(s.Observe().Lifecycle.RetainedActors, Is.EqualTo(4));
            Assert.That(s.Observe().Lifecycle.Active, Is.EqualTo(1));
            Assert.That(s.Observe().Lifecycle.RepositoryCount, Is.EqualTo(1));
            Assert.That(s.CaptureRecording().Trace.Any(t => t.Code == "spawn.budget"), Is.True);
        }
        [Test]
        public void RandomIsConsumedOnlyOnEnemyBirthAndResetRestoresIt()
        {
            GameplayScenario scenario = new GameplayScenario(respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40, seed: 42);
            using ModernSession s = new GameplayDefinition().CreateTestSession(scenario);
            ulong initialState = s.Observe().EnemyRandomState;
            int initialHealth = s.Observe().Actors[1].Health;
            string initialHash = s.CaptureRecording().InitialHash;
            s.Gameplay.Submit(s.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 999)); s.Simulation.Step();
            Assert.That(s.Observe().EnemyRandomState, Is.EqualTo(initialState));
            Assert.That(s.Observe().Actors[0].Health, Is.EqualTo(30));
            Assert.That(initialHealth, Is.InRange(20, 40));
            s.Admin.Reset(scenario);
            Assert.That(s.Observe().EnemyRandomState, Is.EqualTo(initialState));
            Assert.That(s.Observe().Actors[1].Health, Is.EqualTo(initialHealth));
            Assert.That(s.CaptureRecording().InitialHash, Is.EqualTo(initialHash));
        }
        [TestCase(1f / 30)]
        [TestCase(1f / 60)]
        [TestCase(1f / 144)]
        [TestCase(.37f)]
        public void RepeatedRandomRespawnsReplayAcrossFrameSchedules(float frameDelta)
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true,
                enemyHealthMin: 20, enemyHealthMax: 40, maxEnemySpawns: 10, seed: 77);
            using ModernSession first = new GameplayDefinition().CreateTestSession(scenario);
            using ModernSession reversed = new GameplayDefinition().CreateTestSession(scenario);
            for (ulong tick = 1; tick <= 8; tick++) first.Gameplay.Submit(first.Id, tick, tick, new GameplayInput(GameplayActionKind.Attack, 1, tick + 1));
            for (ulong tick = 8; tick > 0; tick--) reversed.Gameplay.Submit(reversed.Id, tick, tick, new GameplayInput(GameplayActionKind.Attack, 1, tick + 1));
            for (int tick = 0; tick < 12; tick++) Assert.That(first.Simulation.Step().Hash, Is.EqualTo(reversed.Simulation.Step().Hash));
            Assert.That(first.Observe().Lifecycle.EnemiesSpawned, Is.EqualTo(9));
            Assert.That(first.Observe().Actors.Skip(1).Select(a => a.MaxHealth).Distinct().Count(), Is.GreaterThan(1));
            using (MemoryStream stream = new MemoryStream())
            {
                TemplateRecordingIO.Write(stream, first.CaptureRecording()); stream.Position = 0;
                using ModernReplay replay = new GameplayDefinition().CreateReplay(TemplateRecordingIO.Read(stream)); replay.Play();
                for (int i = 0; i < 10000 && replay.State == TemplateReplayState.Playing; i++) replay.AdvanceTime(frameDelta);
                Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
                Assert.That(replay.FirstDifference, Is.Null);
                Assert.That(replay.Observe().EnemyRandomState, Is.EqualTo(first.Observe().EnemyRandomState));
            }
        }
        [Test]
        public void DifferentSeedsProduceDifferentHealthSequences()
        {
            int[] values = Enumerable.Range(1, 16).Select(seed =>
            {
                using ModernSession session = new GameplayDefinition().CreateTestSession(
                    new GameplayScenario(enemyHealthMin: 20, enemyHealthMax: 40, seed: (ulong)seed));
                return session.Observe().FindActor(2).Health;
            }).ToArray();
            Assert.That(values.Distinct().Count(), Is.GreaterThan(1));
        }
        [Test]
        public void FailurePreservesFirstEvidenceStageAndCancelsFutureInputs()
        {
            using ModernSession s = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            s.Simulation.Step();
            s.Gameplay.Submit(s.Id, 1, 2, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            s.Gameplay.Submit(s.Id, 2, 3, new GameplayInput(GameplayActionKind.Move, 1)); s.Simulation.Step();
            TemplateFailure saved = s.Failure;
            TemplateRecording recording = s.CaptureRecording();
            Assert.That(saved.Stage, Is.EqualTo("PrePhysics"));
            Assert.That(saved.LastCompletedTick, Is.EqualTo(1));
            Assert.That(saved.Sequence, Is.Zero);
            Assert.That(s.Results.Find(s.Id, 2).CancellationReason, Is.EqualTo("session.faulted"));
            Assert.Throws<InvalidOperationException>(() => s.Simulation.Step()); s.Admin.Stop();
            Assert.That(s.Failure, Is.SameAs(saved));
            using ModernReplay replay = new GameplayDefinition().CreateReplay(recording);
            replay.Step(); replay.Step();
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.ReproducedFailure));
        }
        [Test]
        public void StopCancellationReasonIsDistinctFromFault()
        {
            using ModernSession s = new GameplayDefinition().CreateTestSession(new GameplayScenario());
            s.Gameplay.Submit(s.Id, 1, 5, new GameplayInput(GameplayActionKind.Attack, 1, 2)); s.Admin.Stop();
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
