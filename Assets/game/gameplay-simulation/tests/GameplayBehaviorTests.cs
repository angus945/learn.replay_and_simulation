using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterCombat;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using InvariantChecks;
using MovementDemo;
using NUnit.Framework;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplaySimulation.Tests
{
    public sealed class GameplayBehaviorTests
    {
        [Test]
        public void SubmissionIsNotExecutionAndTargetTickIsExact()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f));
            SubmissionResult result = session.Gameplay.Submit(session.Id, 1, 2, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            Assert.That(result.Queued, Is.True);
            Assert.That(session.Observe().FindActor(1).X, Is.Zero);
            Assert.That(session.Simulation.Step().Results, Is.Empty);
            TemplateTick second = session.Simulation.Step();
            Assert.That(second.Results.Single().Status, Is.EqualTo(ActionStatus.Accepted));
            Assert.That(session.Observe().FindActor(1).X, Is.EqualTo(1));
        }

        [Test]
        public void SameTickUsesSequenceNotSubmissionOrder()
        {
            GameplayDefinition definition = new GameplayDefinition();
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            using ModernSession first = definition.CreateTestSession(scenario);
            using ModernSession second = definition.CreateTestSession(scenario);
            first.Gameplay.Submit(first.Id, 2, 1, new GameplayInput(GameplayActionKind.Move, 1, x: -1));
            first.Gameplay.Submit(first.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            second.Gameplay.Submit(second.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            second.Gameplay.Submit(second.Id, 2, 1, new GameplayInput(GameplayActionKind.Move, 1, x: -1));
            TemplateTick firstTick = first.Simulation.Step();
            TemplateTick secondTick = second.Simulation.Step();
            Assert.That(firstTick.Results.Select(result => result.Sequence), Is.EqualTo(new ulong[] { 1, 2 }));
            Assert.That(firstTick.Hash, Is.EqualTo(secondTick.Hash));
            Assert.That(first.Observe().FindActor(1).X, Is.EqualTo(-1));
        }

        [Test]
        public void InvalidActionsProduceStableResultsAndDoNotMutateActors()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 999, x: 1));
            session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Move, 1, x: float.NaN));
            session.Gameplay.Submit(session.Id, 3, 1, new GameplayInput((GameplayActionKind)99, 1));
            TemplateTick report = session.Simulation.Step();
            Assert.That(report.Results.Select(result => result.Code), Is.EqualTo(new[] { "actor.unknown", "parameters.invalid", "action.unknown" }));
            Assert.That(report.Results[1].Status, Is.EqualTo(ActionStatus.InvalidRequest));
            Assert.That(session.Observe().FindActor(1).X, Is.Zero);
            Assert.That(session.State, Is.EqualTo(SessionState.Running));
            Assert.That(session.Failure, Is.Null);
        }

        [Test]
        public void QueueRejectsDuplicateStaleAndPastTickInputs()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario());
            GameplayInput move = new GameplayInput(GameplayActionKind.Move, 1, x: 1);
            session.Gameplay.Submit(session.Id, 1, 1, move);
            Assert.That(session.Gameplay.Submit(session.Id, 1, 1, move).Code, Is.EqualTo("sequence.invalid_or_duplicate"));
            Assert.That(session.Gameplay.Submit("old", 1, 1, move).Code, Is.EqualTo("session.stale"));
            Assert.That(session.Gameplay.Submit(session.Id, 0, 1, move).Code, Is.EqualTo("sequence.invalid_or_duplicate"));
            Assert.That(session.Gameplay.Submit(session.Id, 2, 0, move).Code, Is.EqualTo("tick.out_of_range"));
            session.Simulation.Step();
            Assert.That(session.Gameplay.Submit(session.Id, 3, 1, move).Code, Is.EqualTo("tick.out_of_range"));
        }

        [Test]
        public void ResetRebuildsWorldAndInvalidatesOldSessionInputs()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            using ModernSession session = new GameplayDefinition().CreateTestSession(scenario);
            string oldId = session.Id;
            GameplayInput move = new GameplayInput(GameplayActionKind.Move, 1, x: 1);
            session.Gameplay.Submit(session.Id, 1, 1, move);
            session.Gameplay.Submit(session.Id, 99, 3, move);
            session.Simulation.Step();
            session.Admin.Reset(scenario);
            Assert.That(session.CurrentTick, Is.Zero);
            Assert.That(session.Observe().FindActor(1).X, Is.Zero);
            Assert.That(session.CaptureRecording().Inputs, Is.Empty);
            Assert.That(session.Gameplay.Submit(oldId, 99, 3, move).Code, Is.EqualTo("session.stale"));
            Assert.That(session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1)).Queued, Is.True);
            session.Simulation.Step(); session.Simulation.Step(); session.Simulation.Step();
            Assert.That(session.Observe().FindActor(1).X, Is.Zero);
            Assert.That(session.CaptureRecording().Ticks.Count, Is.EqualTo(3));
        }

        [Test]
        public void StopRejectsGameplayAndOnlyAdminCanReset()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario());
            session.Admin.Stop();
            Assert.That(session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1)).Code,
                Is.EqualTo("session.not_running"));
            Assert.Throws<InvalidOperationException>(() => session.Simulation.Step());
            Assert.That(typeof(ITemplateGameplay<GameplayInput, GameplayObservation>).GetMethod("Reset"), Is.Null);
            session.Admin.Reset(new GameplayScenario());
            Assert.That(session.State, Is.EqualTo(SessionState.Running));
        }

        [Test]
        public void ObservationIsOwnedAndSessionsAreIsolated()
        {
            GameplayDefinition definition = new GameplayDefinition();
            using ModernSession first = definition.CreateTestSession(new GameplayScenario(tickDelta: .25f));
            using ModernSession second = definition.CreateTestSession(new GameplayScenario(tickDelta: .25f));
            GameplayObservation initial = first.Observe();
            first.Gameplay.Submit(first.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            first.Simulation.Step();
            Assert.That(initial.FindActor(1).X, Is.Zero);
            Assert.That(second.CurrentTick, Is.Zero);
            Assert.That(second.Observe().FindActor(1).X, Is.Zero);
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        }

        [Test]
        public void AttackDamageDeathAndStructuralCommitOccurOnce()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f, health: 10));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 3, 1, new GameplayInput(GameplayActionKind.Move, 2, x: 1));
            TemplateTick report = session.Simulation.Step();
            Assert.That(report.Results.Select(result => result.Code), Is.EqualTo(new[] { "attack.applied", "target.dead", "actor.dead" }));
            ActorObservation enemy = session.Observe().FindActor(2);
            Assert.That(enemy.Health, Is.Zero);
            Assert.That(enemy.Active, Is.False);
            Assert.That(enemy.X, Is.EqualTo(1));
            IReadOnlyList<TraceEntry> trace = session.CaptureRecording().Trace;
            Assert.That(trace.Count(entry => entry.Type == "ActorDied"), Is.EqualTo(1));
            Assert.That(trace.Count(entry => entry.Type == "ActorDamaged"), Is.EqualTo(1));
            Assert.That(trace.Any(entry => entry.Stage == "InternalCommand" && entry.Wave >= 0), Is.True);
            Assert.That(trace.Any(entry => entry.Stage == "Phase" && entry.Type == "StructuralCommit" && entry.Code == "end"), Is.True);
            Assert.That(trace.Count(entry => entry.Stage == "DomainEvent" && entry.Type == "Destroyed" && entry.Actor == enemy.Id), Is.EqualTo(1));
            session.Simulation.Step();
            Assert.That(session.Observe().FindActor(2).X, Is.EqualTo(enemy.X));
        }

        [Test]
        public void DeadActorCannotAttackLaterInSameTick()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(health: 10));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Attack, 2, 1));
            Assert.That(session.Simulation.Step().Results[1].Code, Is.EqualTo("actor.dead"));
            Assert.That(session.Observe().FindActor(1).Health, Is.EqualTo(10));
        }

        [Test]
        public void AttackChecksRangeSelfAndUnknownTargetWithoutDamage()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(attackRange: .5f));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Attack, 1, 1));
            session.Gameplay.Submit(session.Id, 3, 1, new GameplayInput(GameplayActionKind.Attack, 1, 999));
            Assert.That(session.Simulation.Step().Results.Select(result => result.Code),
                Is.EqualTo(new[] { "target.out_of_range", "target.self", "target.unknown" }));
            Assert.That(session.Observe().FindActor(2).Health, Is.EqualTo(30));
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
            using MovementDemoSession demo = new MovementDemoSession(new View(), 4, .25f);
            using ModernSession direct = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f, includeEnemy: false));
            demo.CaptureAxes(1, 1); demo.AdvanceTime(.5f);
            direct.Gameplay.Submit(direct.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1, y: 1));
            direct.Simulation.Step(); direct.Simulation.Step();
            Assert.That(demo.CurrentPosition.X, Is.EqualTo(direct.Observe().FindActor(1).X));
            Assert.That(demo.CurrentPosition.Y, Is.EqualTo(direct.Observe().FindActor(1).Y));
        }

        [Test]
        public void CanonicalHashIncludesHealthAndDesiredDirectionWithUnchangedPositions()
        {
            GameplayDefinition definition = new GameplayDefinition();
            GameplayScenario scenario = new GameplayScenario(speed: 0);
            using ModernSession idle = definition.CreateTestSession(scenario);
            using ModernSession moving = definition.CreateTestSession(scenario);
            using ModernSession damaged = definition.CreateTestSession(scenario);
            moving.Gameplay.Submit(moving.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            damaged.Gameplay.Submit(damaged.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            string idleHash = idle.Simulation.Step().Hash;
            string movingHash = moving.Simulation.Step().Hash;
            string damagedHash = damaged.Simulation.Step().Hash;
            Assert.That(moving.Observe().FindActor(1).X, Is.EqualTo(idle.Observe().FindActor(1).X));
            Assert.That(moving.Observe().FindActor(1).DirectionX, Is.EqualTo(1));
            Assert.That(damaged.Observe().FindActor(2).Health, Is.EqualTo(20));
            Assert.That(movingHash, Is.Not.EqualTo(idleHash));
            Assert.That(damagedHash, Is.Not.EqualTo(idleHash));
            Assert.That(idle.Observe().Actors.Select(actor => actor.Id), Is.Ordered);
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
            GameplayDefinition definition = new GameplayDefinition(new Func<IInvariant<GameplayObservation>>[] { () => new PositionLimit() },
                "test/position-limit-v1");
            using ModernSession session = definition.CreateTestSession(new GameplayScenario(tickDelta: .25f, traceCapacity: 3));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            session.Simulation.Step();
            Assert.That(session.InvariantReport.Violations.Single().Code, Is.EqualTo("test.position_limit"));
            Assert.That(session.State, Is.EqualTo(SessionState.Faulted));
            Assert.That(session.Failure.Tick, Is.EqualTo(1));
            TemplateRecording saved = session.CaptureRecording();
            Assert.That(saved.Inputs.Count, Is.EqualTo(1));
            Assert.That(saved.Trace.Count, Is.EqualTo(3));
            Assert.That(saved.DroppedTraceEntries, Is.GreaterThan(0));
            Assert.That(saved.Failure.ExceptionType, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Simulation.Step());
            session.Admin.Reset(new GameplayScenario());
            Assert.That(session.Failure, Is.Null);
            Assert.That(saved.Inputs.Count, Is.EqualTo(1));
            Assert.That(saved.Failure.Code, Is.EqualTo("test.position_limit"));
        }

        [Test]
        public void ExceptionFaultsSessionAndRecordsReproducibleInput()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            session.Simulation.Step();
            Assert.That(session.State, Is.EqualTo(SessionState.Faulted));
            Assert.That(session.Failure.Code, Is.EqualTo("simulation.exception"));
            Assert.That(session.Failure.ExceptionType, Is.EqualTo(typeof(ArgumentOutOfRangeException).FullName));
            Assert.That(session.Failure.Sequence, Is.Zero);
            Assert.That(session.Gameplay.Submit(session.Id, 2, 2, new GameplayInput(GameplayActionKind.Move, 1)).Queued, Is.False);
        }

        [Test]
        public void RecordingRoundTripReproducesFailureAndCompletedResults()
        {
            GameplayDefinition definition = new GameplayDefinition();
            using ModernSession first = definition.CreateTestSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue, build: "test-build"));
            first.Gameplay.Submit(first.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            first.Simulation.Step();
            using MemoryStream stream = new MemoryStream();
            TemplateRecordingIO.Write(stream, first.CaptureRecording()); stream.Position = 0;
            TemplateRecording saved = TemplateRecordingIO.Read(stream);
            using ModernReplay replay = definition.CreateReplay(saved);
            replay.Step();
            Assert.That(saved.Schema, Is.EqualTo(1));
            Assert.That(saved.Ticks[0].Hash, Is.Null);
            Assert.That(saved.Ticks[0].Results.Single().Code, Is.EqualTo("move.applied"));
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.ReproducedFailure));
            Assert.That(replay.FirstDifference, Is.Null);
            Assert.That(replay.Diagnostics.ObserveDiagnostics().FaultCode, Is.EqualTo(saved.Failure.Code));
        }

        [Test]
        public void IdenticalScenarioAndInputsReproduceEveryTickHash()
        {
            GameplayDefinition definition = new GameplayDefinition();
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            using ModernSession first = definition.CreateTestSession(scenario);
            using ModernSession second = definition.CreateTestSession(scenario);
            GameplayInput[] inputs = { new GameplayInput(GameplayActionKind.Attack, 1, 2),
                new GameplayInput(GameplayActionKind.Move, 1, x: 1), new GameplayInput(GameplayActionKind.Move, 1) };
            ulong[] targetTicks = { 1, 2, 4 };
            for (int index = 0; index < inputs.Length; index++)
                first.Gameplay.Submit(first.Id, (ulong)index + 1, targetTicks[index], inputs[index]);
            for (int index = inputs.Length - 1; index >= 0; index--)
                second.Gameplay.Submit(second.Id, (ulong)index + 1, targetTicks[index], inputs[index]);
            for (int tick = 0; tick < 8; tick++)
            {
                TemplateTick expected = first.Simulation.Step();
                TemplateTick actual = second.Simulation.Step();
                Assert.That(actual.Hash, Is.EqualTo(expected.Hash));
                Assert.That(actual.Results.Select(result => result.Code), Is.EqualTo(expected.Results.Select(result => result.Code)));
            }
        }

        [Test]
        public void InvariantRecordingRequiresTheSamePolicyToReproduce()
        {
            GameplayDefinition definition = new GameplayDefinition(new Func<IInvariant<GameplayObservation>>[] { () => new PositionLimit() },
                "test/position-limit-v1");
            using ModernSession first = definition.CreateTestSession(new GameplayScenario(tickDelta: .25f));
            first.Gameplay.Submit(first.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            first.Simulation.Step();
            TemplateRecording recording = first.CaptureRecording();
            using ModernReplay incompatible = new GameplayDefinition().CreateReplay(recording);
            Assert.That(incompatible.State, Is.EqualTo(TemplateReplayState.Diverged));
            Assert.That(incompatible.FirstDifference.Category, Is.EqualTo("policy"));
            Assert.That(incompatible.CurrentTick, Is.Zero);
            using ModernReplay compatible = definition.CreateReplay(recording);
            compatible.Step();
            Assert.That(compatible.State, Is.EqualTo(TemplateReplayState.ReproducedFailure));
        }

        [Test]
        public void LimitsBoundHistoryAndTickExecution()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(maxTicks: 1, maxActions: 1));
            Assert.That(session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1)).Queued, Is.True);
            Assert.That(session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Move, 1)).Code, Is.EqualTo("input.capacity"));
            session.Simulation.Step();
            Assert.Throws<InvalidOperationException>(() => session.Simulation.Step());
            Assert.That(session.State, Is.EqualTo(SessionState.Stopped));
            Assert.That(session.CaptureRecording().Inputs.Count, Is.EqualTo(1));
        }

        private sealed class PositionLimit : IInvariant<GameplayObservation>
        {
            public string Code => "test.position_limit";
            public InvariantViolation Evaluate(GameplayObservation observation)
                => observation.FindActor(observation.PlayerId).X > .5f ? new InvariantViolation(Code, "Injected test scenario boundary.") : null;
        }
        private sealed class View : ICharacterMovementView
        {
            public void SetPosition(MovementPosition position) { }
        }
    }
}
