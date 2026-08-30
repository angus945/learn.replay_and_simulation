using System;
using Invariants;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterCombat;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using MovementDemo;
using NUnit.Framework;
using Testability;

namespace GameplaySimulation.Tests
{
    public sealed class GameplaySessionTests
    {
        private static GameplaySession NewSession(GameplayScenario scenario = null)
        {
            GameplaySession session = new GameplaySession();
            session.Start(scenario ?? new GameplayScenario(tickDelta: .25f));
            return session;
        }
        private static GameplayRequest Move(GameplaySession session, ulong sequence, ulong tick, float x = 1, float y = 0, ulong actor = 1)
            => new GameplayRequest(session.Id, sequence, tick, GameplayActionKind.Move, actor, x: x, y: y);
        private static GameplayRequest Attack(GameplaySession session, ulong sequence, ulong tick, ulong actor = 1, ulong target = 2)
            => new GameplayRequest(session.Id, sequence, tick, GameplayActionKind.Attack, actor, target);

        [Test]
        public void SubmissionIsNotExecutionAndTargetTickIsExact()
        {
            GameplaySession session = NewSession();
            SubmissionResult result = session.Submit(Move(session, 1, 2));
            Assert.That(result.Queued, Is.True);
            Assert.That(session.Observe().Actors[0].X, Is.Zero);
            Assert.That(session.Step().Results, Is.Empty);
            TickReport second = session.Step();
            Assert.That(second.Results.Single().Status, Is.EqualTo(ActionStatus.Accepted));
            Assert.That(session.Observe().Actors[0].X, Is.EqualTo(1));
        }

        [Test]
        public void SameTickUsesSequenceNotSubmissionOrder()
        {
            GameplaySession a = NewSession();
            GameplaySession b = NewSession();
            a.Submit(Move(a, 2, 1, -1)); a.Submit(Move(a, 1, 1));
            b.Submit(Move(b, 1, 1)); b.Submit(Move(b, 2, 1, -1));
            TickReport ar = a.Step(); TickReport br = b.Step();
            Assert.That(ar.Results.Select(result => result.Sequence), Is.EqualTo(new ulong[] { 1, 2 }));
            Assert.That(ar.StateHash, Is.EqualTo(br.StateHash));
            Assert.That(a.Observe().Actors[0].X, Is.EqualTo(-1));
        }

        [Test]
        public void InvalidActionsProduceStableResultsAndDoNotMutateActors()
        {
            GameplaySession session = NewSession();
            session.Submit(Move(session, 1, 1, actor: 999));
            session.Submit(Move(session, 2, 1, float.NaN));
            session.Submit(new GameplayRequest(session.Id, 3, 1, (GameplayActionKind)99, 1));
            TickReport report = session.Step();
            Assert.That(report.Results.Select(result => result.Code), Is.EqualTo(new[] { "actor.unknown", "parameters.invalid", "action.unknown" }));
            Assert.That(report.Results[1].Status, Is.EqualTo(ActionStatus.InvalidRequest));
            Assert.That(session.Observe().Actors[0].X, Is.Zero);
            Assert.That(session.State, Is.EqualTo(SessionState.Running));
            Assert.That(session.Failure, Is.Null);
        }

        [Test]
        public void QueueRejectsDuplicateStaleAndPastTickRequests()
        {
            GameplaySession session = NewSession();
            GameplayRequest move = Move(session, 1, 1);
            session.Submit(move);
            Assert.That(session.Submit(move).Code, Is.EqualTo("sequence.duplicate"));
            Assert.That(session.Submit(move.InSession("old")).Code, Is.EqualTo("session.stale"));
            Assert.That(session.Submit(Move(session, 2, 0)).Code, Is.EqualTo("tick.out_of_range"));
            session.Step();
            Assert.That(session.Submit(Move(session, 3, 1)).Code, Is.EqualTo("tick.out_of_range"));
        }

        [Test]
        public void ResetRebuildsWorldAndInvalidatesOldSessionRequests()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            GameplaySession session = NewSession(scenario);
            GameplayRequest old = Move(session, 99, 3);
            session.Submit(Move(session, 1, 1)); session.Submit(old); session.Step();
            session.Reset(scenario);
            Assert.That(session.CurrentTick, Is.Zero);
            Assert.That(session.Observe().Actors[0].X, Is.Zero);
            Assert.That(session.ActionHistory, Is.Empty);
            Assert.That(session.Submit(old).Code, Is.EqualTo("session.stale"));
            Assert.That(session.Submit(Move(session, 1, 1, 0)).Queued, Is.True);
            session.Step(); session.Step(); session.Step();
            Assert.That(session.Observe().Actors[0].X, Is.Zero);
            Assert.That(session.HashHistory.Count, Is.EqualTo(4));
        }

        [Test]
        public void StopRejectsGameplayAndStartCannotBeRepeated()
        {
            GameplaySession session = NewSession();
            Assert.Throws<InvalidOperationException>(() => session.Start(new GameplayScenario()));
            Assert.Throws<InvalidOperationException>(() => session.RegisterInvariant(() => new PositionLimit()));
            session.Stop();
            Assert.That(session.Submit(Move(session, 1, 1)).Code, Is.EqualTo("session.not_running"));
            Assert.Throws<InvalidOperationException>(() => session.Step());
            Assert.That(typeof(IGameplayControl).GetMethod("Reset"), Is.Null);
        }

        [Test]
        public void ObservationIsOwnedAndSessionsAreIsolated()
        {
            GameplaySession a = NewSession(); GameplaySession b = NewSession();
            GameplayObservation initial = a.Observe();
            a.Submit(Move(a, 1, 1)); a.Step();
            Assert.That(initial.Actors[0].X, Is.Zero);
            Assert.That(b.CurrentTick, Is.Zero);
            Assert.That(b.Observe().Actors[0].X, Is.Zero);
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
        }

        [Test]
        public void AttackDamageDeathAndStructuralCommitOccurOnce()
        {
            GameplaySession session = NewSession(new GameplayScenario(tickDelta: .25f, health: 10));
            session.Submit(Attack(session, 1, 1));
            session.Submit(Attack(session, 2, 1));
            session.Submit(Move(session, 3, 1, actor: 2));
            TickReport report = session.Step();
            Assert.That(report.Results.Select(result => result.Code), Is.EqualTo(new[] { "attack.applied", "target.dead", "actor.dead" }));
            ActorObservation enemy = session.Observe().Actors[1];
            Assert.That(enemy.Health, Is.Zero);
            Assert.That(enemy.Active, Is.False);
            Assert.That(enemy.X, Is.EqualTo(1));
            IReadOnlyList<TraceEntry> trace = session.ReadTrace();
            Assert.That(trace.Count(entry => entry.Type == "ActorDied"), Is.EqualTo(1));
            Assert.That(trace.Count(entry => entry.Type == "ActorDamaged"), Is.EqualTo(1));
            Assert.That(trace.Any(entry => entry.Stage == "InternalCommand" && entry.Wave >= 0), Is.True);
            Assert.That(trace.Any(entry => entry.Stage == "StructuralCommit"), Is.True);
            session.Step();
            Assert.That(session.Observe().Actors[1].X, Is.EqualTo(enemy.X));
        }

        [Test]
        public void DeadActorCannotAttackLaterInSameTick()
        {
            GameplaySession session = NewSession(new GameplayScenario(health: 10));
            session.Submit(Attack(session, 1, 1));
            session.Submit(Attack(session, 2, 1, 2, 1));
            Assert.That(session.Step().Results[1].Code, Is.EqualTo("actor.dead"));
            Assert.That(session.Observe().Actors[0].Health, Is.EqualTo(10));
        }

        [Test]
        public void AttackChecksRangeSelfAndUnknownTargetWithoutDamage()
        {
            GameplaySession session = NewSession(new GameplayScenario(attackRange: .5f));
            session.Submit(Attack(session, 1, 1));
            session.Submit(Attack(session, 2, 1, target: 1));
            session.Submit(Attack(session, 3, 1, target: 999));
            Assert.That(session.Step().Results.Select(result => result.Code),
                Is.EqualTo(new[] { "target.out_of_range", "target.self", "target.unknown" }));
            Assert.That(session.Observe().Actors[1].Health, Is.EqualTo(30));
        }

        [Test]
        public void CombatDomainClampsOverkillAndRejectsNegativeDamage()
        {
            Combatant combatant = new Combatant(10);
            Assert.That(combatant.TakeDamage(99), Is.EqualTo(10));
            Assert.That(combatant.TakeDamage(99), Is.Zero);
            Assert.That(combatant.IsDead, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => combatant.TakeDamage(-1));
        }

        [Test]
        public void PlayerAdapterAndFormalDriverProduceIdenticalMovement()
        {
            MovementDemoSession demo = new MovementDemoSession(new View(), 4, .25f);
            GameplaySession direct = NewSession(new GameplayScenario(tickDelta: .25f, includeEnemy: false));
            demo.CaptureAxes(1, 1); demo.AdvanceTime(.5f);
            direct.Submit(Move(direct, 1, 1, 1, 1)); direct.Step(); direct.Step();
            Assert.That(demo.CurrentPosition.X, Is.EqualTo(direct.Observe().Actors[0].X));
            Assert.That(demo.CurrentPosition.Y, Is.EqualTo(direct.Observe().Actors[0].Y));
        }

        [Test]
        public void HashNormalizesActorOrderAndIncludesHealthAndDesiredDirection()
        {
            GameplayScenario scenario = new GameplayScenario();
            ActorObservation a = new ActorObservation(1, 0, 0, 0, 0, 4, 10, 10, true);
            ActorObservation b = new ActorObservation(2, 1, 0, 0, 0, 4, 10, 10, true);
            string hash = GameplayStateHasher.Compute(new GameplayObservation(1, new[] { a, b }), scenario);
            Assert.That(GameplayStateHasher.Compute(new GameplayObservation(1, new[] { b, a }), scenario), Is.EqualTo(hash));
            ActorObservation changed = new ActorObservation(1, 0, 0, 1, 0, 4, 10, 10, true);
            Assert.That(GameplayStateHasher.Compute(new GameplayObservation(1, new[] { changed, b }), scenario), Is.Not.EqualTo(hash));
        }

        [Test]
        public void OracleDetectsDeliberatelyMalformedObservationWithoutPrivateStateMutation()
        {
            GameplayInvariant oracle = new GameplayInvariant();
            ActorObservation invalid = new ActorObservation(1, 0, 0, 0, 0, 4, -1, 10, false);
            Assert.That(oracle.Evaluate(new GameplayObservation(0, new[] { invalid })).Code, Is.EqualTo("health.bounds"));
            ActorObservation deadActive = new ActorObservation(1, 0, 0, 0, 0, 4, 0, 10, true);
            Assert.That(oracle.Evaluate(new GameplayObservation(0, new[] { deadActive })).Code, Is.EqualTo("lifecycle.committed"));
        }

        [Test]
        public void InvariantFailureCapturesEvidenceAndRequiresReset()
        {
            GameplaySession session = new GameplaySession();
            session.RegisterInvariant(() => new PositionLimit());
            session.Start(new GameplayScenario(tickDelta: .25f, traceCapacity: 3));
            session.Submit(Move(session, 1, 1));
            TickReport report = session.Step();
            Assert.That(report.Violations.Single().Code, Is.EqualTo("test.position_limit"));
            Assert.That(session.State, Is.EqualTo(SessionState.Faulted));
            Assert.That(session.Failure.FailureTick, Is.EqualTo(1));
            Assert.That(session.Failure.Actions.Count, Is.EqualTo(1));
            Assert.That(session.Failure.Trace.Count, Is.EqualTo(3));
            Assert.That(session.Failure.DroppedTraceEntries, Is.GreaterThan(0));
            Assert.That(session.Failure.Exception, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Step());
            FailureArtifact saved = session.Failure;
            session.Reset(new GameplayScenario());
            Assert.That(session.Failure, Is.Null);
            Assert.That(saved.Actions.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExceptionFaultsSessionAndRecordsReproducibleInput()
        {
            GameplaySession session = NewSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Submit(Move(session, 1, 1));
            session.Step();
            Assert.That(session.State, Is.EqualTo(SessionState.Faulted));
            Assert.That(session.Failure.Code, Is.EqualTo("simulation.exception"));
            Assert.That(session.Failure.Exception, Does.Contain("ArgumentOutOfRangeException"));
            Assert.That(session.Failure.ActionSequence, Is.Zero); // Integration failure, not falsely attributed to last request.
            Assert.That(session.Submit(Move(session, 2, 2)).Queued, Is.False);
        }

        [Test]
        public void ArtifactRoundTripCanRerunSameFailureAndResults()
        {
            GameplaySession first = NewSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue, build: "test-build"));
            first.Submit(Move(first, 1, 1)); first.Step();
            using (MemoryStream stream = new MemoryStream())
            {
                ArtifactJson.Write(stream, first.Failure);
                stream.Position = 0;
                FailureArtifact saved = ArtifactJson.Read<FailureArtifact>(stream);
                GameplaySession second = new GameplaySession();
                ScenarioRerun.Run(saved.Scenario, saved.Actions, (int)saved.FailureTick, second);
                Assert.That(saved.SchemaVersion, Is.EqualTo(1));
                Assert.That(saved.Actors.Count, Is.EqualTo(2));
                Assert.That(ScenarioRerun.VerifyFailure(saved), Is.True);
                Assert.That(second.Failure.Code, Is.EqualTo(saved.Code));
                Assert.That(second.Failure.FailureTick, Is.EqualTo(saved.FailureTick));
                Assert.That(second.Failure.Results.Select(result => result.Code), Is.EqualTo(saved.Results.Select(result => result.Code)));
                Assert.That(second.Failure.Hashes.Select(hash => hash.Hash), Is.EqualTo(saved.Hashes.Select(hash => hash.Hash)));
            }
        }

        [Test]
        public void IdenticalScenarioAndActionsReproduceEveryTickHash()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            GameplayRequest[] actions = {
                new GameplayRequest("original", 1, 1, GameplayActionKind.Attack, 1, 2),
                new GameplayRequest("original", 2, 2, GameplayActionKind.Move, 1, x: 1),
                new GameplayRequest("original", 3, 4, GameplayActionKind.Move, 1, x: 0)
            };
            IReadOnlyList<TickReport> first = ScenarioRerun.Run(scenario, actions, 8);
            IReadOnlyList<TickReport> second = ScenarioRerun.Run(scenario, actions.Reverse(), 8);
            Assert.That(first.Select(tick => tick.StateHash), Is.EqualTo(second.Select(tick => tick.StateHash)));
            Assert.That(first.SelectMany(tick => tick.Results).Select(result => result.Code),
                Is.EqualTo(second.SelectMany(tick => tick.Results).Select(result => result.Code)));
        }

        [Test]
        public void InvariantArtifactRequiresTheSameDiagnosticPolicyToReproduce()
        {
            GameplaySession first = new GameplaySession();
            first.RegisterInvariant(() => new PositionLimit());
            first.Start(new GameplayScenario(tickDelta: .25f));
            first.Submit(Move(first, 1, 1)); first.Step();
            Assert.That(ScenarioRerun.VerifyFailure(first.Failure), Is.False);
            GameplaySession second = new GameplaySession();
            second.RegisterInvariant(() => new PositionLimit());
            Assert.That(ScenarioRerun.VerifyFailure(first.Failure, second), Is.True);
        }

        [Test]
        public void LimitsBoundHistoryAndTickExecution()
        {
            GameplaySession session = NewSession(new GameplayScenario(maxTicks: 1, maxActions: 1));
            Assert.That(session.Submit(Move(session, 1, 1)).Queued, Is.True);
            Assert.That(session.Submit(Move(session, 2, 1)).Code, Is.EqualTo("action.capacity"));
            session.Step();
            Assert.Throws<InvalidOperationException>(() => session.Step());
            Assert.That(session.State, Is.EqualTo(SessionState.Stopped));
            Assert.That(session.ActionHistory.Count, Is.EqualTo(1));
        }

        private sealed class PositionLimit : IInvariant<GameplayObservation>
        {
            public string Code => "test.position_limit";
            public InvariantViolation Evaluate(GameplayObservation observation)
                => observation.Actors[0].X > .5f ? new InvariantViolation(Code, "Injected test scenario boundary.") : null;
        }
        private sealed class View : ICharacterMovementView
        {
            public void SetPosition(MovementPosition position) { }
        }
    }
}
