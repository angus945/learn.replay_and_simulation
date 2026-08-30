using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplaySimulation.Tests
{
    public sealed class ReplayTests
    {
        private sealed class View : CharacterMovement.Integration.ICharacterMovementView
        {
            public void SetPosition(CharacterMovement.Domain.MovementPosition position) { }
        }

        [Test]
        public void RealtimeDemoRecordingPlaysInSeparateManualSession()
        {
            using MovementDemo.MovementDemoSession live = new MovementDemo.MovementDemoSession(new View(), 4, .125f, true);
            live.RequestAttack(); live.AdvanceTime(.125f);
            live.CaptureAxes(1, 0); live.AdvanceTime(.5f);
            using ModernReplay replay = new GameplayDefinition().CreateReplay(live.CaptureReplay());
            ulong liveTick = live.TickNumber;
            live.CaptureAxes(-1, 0);
            replay.Play(); replay.AdvanceTime(2);
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
            Assert.That(replay.Observe().FindActor(1).X, Is.EqualTo(live.CurrentPosition.X));
            Assert.That(live.TickNumber, Is.EqualTo(liveTick));
        }

        private static TemplateRecording RecordBattle()
        {
            using ModernSession session = new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .125f));
            session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 2, 2, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 3, 3, new GameplayInput(GameplayActionKind.Attack, 1, 2));
            session.Gameplay.Submit(session.Id, 4, 3, new GameplayInput(GameplayActionKind.Move, 2, x: 1));
            session.Gameplay.Submit(session.Id, 5, 4, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            for (int tick = 0; tick < 16; tick++) session.Simulation.Step();
            return session.CaptureRecording();
        }

        [TestCase(1f / 30)]
        [TestCase(1f / 60)]
        [TestCase(1f / 144)]
        [TestCase(.37f)]
        public void ReplayMatchesAcrossFrameRatesIncludingDeathAndInputFreeTail(float delta)
        {
            TemplateRecording recording = RecordBattle();
            using ModernReplay replay = new GameplayDefinition().CreateReplay(recording);
            replay.Play();
            for (int frame = 0; frame < 10000 && replay.State == TemplateReplayState.Playing; frame++) replay.AdvanceTime(delta);
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
            Assert.That(replay.FirstDifference, Is.Null);
            Assert.That(replay.Observe().Tick, Is.EqualTo(16));
            Assert.That(replay.Observe().FindActor(2).Active, Is.False);
            Assert.That(replay.Observe().FindActor(1).X, Is.EqualTo(6.5f));
            Assert.That(recording.Ticks[2].Results[1].Code, Is.EqualTo("actor.dead"));
            Assert.That(replay.PresentationAlpha, Is.EqualTo(1));
        }

        [Test]
        public void PauseStepRestartAndEndDoNotAdvanceUnexpectedly()
        {
            using ModernReplay replay = new GameplayDefinition().CreateReplay(RecordBattle());
            replay.AdvanceTime(1);
            Assert.That(replay.Observe().Tick, Is.Zero);
            replay.Step();
            Assert.That(replay.Observe().Tick, Is.EqualTo(1));
            replay.Play(); replay.AdvanceTime(.25f); replay.Pause();
            ulong tick = replay.Observe().Tick;
            replay.AdvanceTime(10);
            Assert.That(replay.Observe().Tick, Is.EqualTo(tick));
            replay.Restart();
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Paused));
            Assert.That(replay.Observe().Tick, Is.Zero);
            replay.Play(); replay.AdvanceTime(10);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(replay.Observe().Tick, Is.EqualTo(16));
            Assert.Throws<InvalidOperationException>(() => replay.Step());
        }

        [Test]
        public void SnapshotIsIndependentOfLaterRecordingAndPendingInputs()
        {
            GameplayDefinition definition = new GameplayDefinition();
            using ModernSession original = definition.CreateTestSession(new GameplayScenario(tickDelta: .125f));
            original.Gameplay.Submit(original.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            original.Gameplay.Submit(original.Id, 2, 30, new GameplayInput(GameplayActionKind.Move, 1));
            for (int tick = 0; tick < 16; tick++) original.Simulation.Step();
            TemplateRecording saved = original.CaptureRecording();
            original.Simulation.Step(); original.Admin.Reset(new GameplayScenario());
            using ModernReplay replay = definition.CreateReplay(saved);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(saved.Ticks.Count, Is.EqualTo(16));
            Assert.That(saved.Inputs.Count, Is.EqualTo(2));
            Assert.That(saved.Inputs[1].Tick, Is.EqualTo(30));
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
            Assert.That(replay.Observe().FindActor(1).X, Is.EqualTo(8));
            Assert.That(original.CurrentTick, Is.Zero);
        }

        [Test]
        public void ZeroTickAndFaultedRecordingsAreBothSupported()
        {
            GameplayDefinition definition = new GameplayDefinition();
            using ModernSession original = definition.CreateTestSession(new GameplayScenario());
            using ModernReplay empty = definition.CreateReplay(original.CaptureRecording());
            Assert.That(empty.State, Is.EqualTo(TemplateReplayState.Completed));
            original.Admin.Reset(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            original.Gameplay.Submit(original.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
            original.Simulation.Step();
            TemplateRecording failed = original.CaptureRecording();
            using ModernReplay replay = definition.CreateReplay(failed);
            replay.Step();
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.ReproducedFailure));
            Assert.That(failed.Failure.Stage, Is.EqualTo("PrePhysics"));
        }

        [Test]
        public void RecordingCodecRetainsCallerStreamsAndEnforcesReadBounds()
        {
            TemplateRecording saved = RecordBattle();
            using MemoryStream bytes = new MemoryStream();
            TemplateRecordingIO.Write(bytes, saved);
            Assert.That(bytes.CanWrite, Is.True);
            bytes.Position = 0;
            TemplateRecording loaded = TemplateRecordingIO.Read(bytes);
            Assert.That(bytes.CanRead, Is.True);
            Assert.That(loaded.Ticks.Select(tick => tick.Hash), Is.EqualTo(saved.Ticks.Select(tick => tick.Hash)));
            using ModernReplay replay = new GameplayDefinition().CreateReplay(loaded);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
            bytes.Position = 0;
            Assert.Throws<ArgumentException>(() => TemplateRecordingIO.Read(bytes, 1));
            Assert.That(bytes.CanRead, Is.True);
        }

        [Test]
        public void FirstChangedCheckpointStopsAtExactTick()
        {
            TemplateRecording saved = RecordBattle();
            TemplateTick[] ticks = saved.Ticks.ToArray();
            ticks[4] = new TemplateTick(5, "different", ticks[4].Results);
            TemplateRecording changed = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, ticks, saved.Failure, saved.Trace, saved.DroppedTraceEntries);
            using ModernReplay replay = new GameplayDefinition().CreateReplay(changed);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Diverged));
            Assert.That(replay.FirstDifference.Tick, Is.EqualTo(5));
            Assert.That(replay.FirstDifference.Category, Is.EqualTo("state_hash"));
            Assert.That(replay.Observe().Tick, Is.EqualTo(5));
        }

        [Test]
        public void ResultAndPolicyDifferencesAreReported()
        {
            TemplateRecording saved = RecordBattle();
            TemplateTick[] ticks = saved.Ticks.ToArray();
            ticks[0] = new TemplateTick(1, ticks[0].Hash, new[] { new ActionResult(1, 1, ActionStatus.Rejected, "changed") });
            TemplateRecording changed = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, ticks, saved.Failure, saved.Trace, saved.DroppedTraceEntries);
            using ModernReplay replay = new GameplayDefinition().CreateReplay(changed);
            replay.Step();
            Assert.That(replay.FirstDifference.Category, Is.EqualTo("action_result"));
            using ModernReplay policy = new GameplayDefinition(null, "different").CreateReplay(saved);
            Assert.That(policy.FirstDifference.Category, Is.EqualTo("policy"));
            Assert.That(policy.Observe().Tick, Is.Zero);
        }

        [Test]
        public void MalformedAndIncompleteHistoryCannotPlay()
        {
            TemplateRecording saved = RecordBattle();
            TemplateRecording missingTick = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, saved.Ticks.Skip(1), null, saved.Trace, saved.DroppedTraceEntries);
            Assert.Throws<ArgumentException>(() => new GameplayDefinition().CreateReplay(missingTick));
            TemplateTick[] ticks = saved.Ticks.ToArray();
            ticks[0] = new TemplateTick(1, ticks[0].Hash, Array.Empty<ActionResult>());
            TemplateRecording missingResult = new TemplateRecording(saved.Policy, saved.Runtime, saved.Scenario, saved.TickDelta, saved.Limits,
                saved.InitialHash, saved.Inputs, ticks, null, saved.Trace, saved.DroppedTraceEntries);
            Assert.Throws<ArgumentException>(() => new GameplayDefinition().CreateReplay(missingResult));
        }
    }
}
