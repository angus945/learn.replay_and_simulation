using System;
using System.Linq;
using DeterministicSimulation.Framework;
using NUnit.Framework;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplaySimulation.Tests
{
    public sealed class ToolControlTests
    {
        [Test]
        public void PortsDoNotExposeEachOthersAuthority()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario());
            Assert.That(session.Gameplay, Is.Not.InstanceOf<ITemplateAdmin<GameplayScenario>>());
            Assert.That(session.Gameplay, Is.Not.InstanceOf<ITemplateSimulation>());
            Assert.That(session.Simulation, Is.Not.InstanceOf<ITemplateGameplay<GameplayInput, GameplayObservation>>());
            Assert.That(session.Results, Is.Not.InstanceOf<ITemplateGameplay<GameplayInput, GameplayObservation>>());
            Assert.That(session.Admin, Is.Not.InstanceOf<ITemplateSimulation>());
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<ITemplateSimulation>());
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<ModernSession>());
        }

        [Test]
        public void RealtimeClockCannotBeDrivenManuallyOrClaimedTwice()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f));
            using (RealtimeSimulationRunner clock = session.CreateRealtimeRunner())
            {
                Assert.Throws<InvalidOperationException>(() => session.Step());
                Assert.Throws<InvalidOperationException>(() => session.Simulation.Step());
                Assert.Throws<InvalidOperationException>(() => session.CreateRealtimeRunner());
                Assert.Throws<InvalidOperationException>(() => session.Admin.Reset(new GameplayScenario()));
                Assert.Throws<InvalidOperationException>(() => session.Dispose());
                Assert.That(session.CurrentTick, Is.Zero);
                Assert.That(clock.AdvanceTime(.25f), Is.EqualTo(1));
                Assert.That(session.CurrentTick, Is.EqualTo(1));
            }
            session.Admin.Reset(new GameplayScenario());
            session.Simulation.Step();
            Assert.That(session.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void ManualSessionCanLeaseRealtimeAndResumeManualAfterRelease()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f));
            session.Simulation.Step();
            using (RealtimeSimulationRunner clock = session.CreateRealtimeRunner())
                Assert.That(clock.AdvanceTime(.5f), Is.EqualTo(2));
            session.Simulation.Step();
            Assert.That(session.CurrentTick, Is.EqualTo(4));
        }

        [Test]
        public void ResultsAreQueryableWithoutTraceAndPagesUseCompletionOrder()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(traceCapacity: 1));
            session.Gameplay.Submit(session.Id, 20, 1, new GameplayInput(GameplayActionKind.Move, 1));
            session.Gameplay.Submit(session.Id, 10, 2, new GameplayInput(GameplayActionKind.Move, 1));
            Assert.That(session.Results.Find(session.Id, 20).State, Is.EqualTo("Pending"));
            session.Simulation.Step(); session.Simulation.Step();
            TemplateActionResultPage first = session.Results.Read(session.Id, 0, 1);
            TemplateActionResultPage second = session.Results.Read(session.Id, first.NextIndex, 1);
            Assert.That(first.Items[0].Sequence, Is.EqualTo(20));
            Assert.That(first.HasMore, Is.True);
            Assert.That(second.Items[0].Sequence, Is.EqualTo(10));
            Assert.That(second.HasMore, Is.False);
            Assert.That(session.Results.Find(session.Id, 20).Result.Code, Is.EqualTo("move.applied"));
            Assert.That(session.Results.Find(session.Id, 999).State, Is.EqualTo("Unknown"));
            Assert.That(session.CurrentTick, Is.EqualTo(2));
            Assert.That(session.CaptureRecording().Trace.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Results.Read(session.Id, 0, 1025));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Results.Read(session.Id, 3, 1));
        }

        [Test]
        public void StopCancelsPendingAndResetInvalidatesResultsAndCursors()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario());
            string oldId = session.Id;
            session.Gameplay.Submit(oldId, 1, 2, new GameplayInput(GameplayActionKind.Move, 1));
            session.Admin.Stop();
            Assert.That(session.Results.Find(oldId, 1).State, Is.EqualTo("Cancelled"));
            session.Admin.Reset(new GameplayScenario());
            Assert.That(session.Results.Find(oldId, 1).State, Is.EqualTo("StaleSession"));
            Assert.Throws<ArgumentException>(() => session.Results.Read(oldId, 0, 1));
        }

        [TestCase("state_hash")]
        [TestCase("action_result")]
        [TestCase("failure")]
        public void ReplayReportsTheFirstChangedEvidence(string category)
        {
            TemplateRecording original = RecordFailure();
            TemplateTick tick = original.Ticks[0];
            TemplateFailure failure = original.Failure;
            if (category == "state_hash") tick = new TemplateTick(1, "changed", tick.Results);
            if (category == "action_result") tick = new TemplateTick(1, tick.Hash, new[] { new ActionResult(1, 1, ActionStatus.Rejected, "changed") });
            if (category == "failure") failure = new TemplateFailure(failure.Tick, failure.LastCompletedTick, 42,
                failure.Stage, failure.Code, failure.ExceptionType, failure.Detail);
            TemplateRecording changed = new TemplateRecording(original.Policy, original.Runtime, original.Scenario, original.TickDelta,
                original.Limits, original.InitialHash, original.Inputs, new[] { tick }, failure, original.Trace, original.DroppedTraceEntries);
            using ModernReplay replay = new GameplayDefinition().CreateReplay(changed);
            replay.Step();
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Diverged));
            Assert.That(replay.FirstDifference.Tick, Is.EqualTo(1));
            Assert.That(replay.FirstDifference.Category, Is.EqualTo(category));
        }

        [Test]
        public void ReplayDistinguishesPolicyMismatchFromRuntimeWarning()
        {
            TemplateRecording original = RecordFailure();
            TemplateRecording changedRuntime = new TemplateRecording(original.Policy, "different-runtime", original.Scenario, original.TickDelta,
                original.Limits, original.InitialHash, original.Inputs, original.Ticks, original.Failure, original.Trace, original.DroppedTraceEntries);
            using ModernReplay matched = new GameplayDefinition().CreateReplay(changedRuntime);
            matched.Step();
            Assert.That(matched.State, Is.EqualTo(TemplateReplayState.ReproducedFailure));
            Assert.That(matched.Warnings, Does.Contain("runtime.mismatch"));
            using ModernReplay mismatch = new GameplayDefinition(null, "different-policy").CreateReplay(original);
            Assert.That(mismatch.State, Is.EqualTo(TemplateReplayState.Diverged));
            Assert.That(mismatch.FirstDifference.Category, Is.EqualTo("policy"));
            Assert.That(mismatch.CurrentTick, Is.Zero);
        }

        [Test]
        public void ReplayCreatesItsOwnSessionAndRejectsMissingRecording()
        {
            GameplayDefinition definition = new GameplayDefinition();
            Assert.Throws<ArgumentNullException>(() => definition.CreateReplay(null));
            using ModernSession live = definition.CreateTestSession(new GameplayScenario());
            live.Simulation.Step();
            using ModernReplay replay = definition.CreateReplay(live.CaptureRecording());
            Assert.That(replay.CurrentTick, Is.Zero);
            replay.Step();
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
            Assert.That(live.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void FaultCancelsFutureInputsButPreservesCompletedResults()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            session.Gameplay.Submit(session.Id, 2, 2, new GameplayInput(GameplayActionKind.Move, 1));
            session.Simulation.Step();
            Assert.That(session.Results.Find(session.Id, 1).State, Is.EqualTo("Completed"));
            Assert.That(session.Results.Find(session.Id, 1).Result.Status, Is.EqualTo(ActionStatus.Accepted));
            Assert.That(session.Results.Find(session.Id, 2).State, Is.EqualTo("Cancelled"));
            Assert.That(session.Results.Find(session.Id, 2).CancellationReason, Is.EqualTo("session.faulted"));
        }

        private static TemplateRecording RecordFailure()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            session.Simulation.Step();
            return session.CaptureRecording();
        }
    }
}
