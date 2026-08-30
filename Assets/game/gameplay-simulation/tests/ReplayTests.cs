using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Testability;

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
            MovementDemo.MovementDemoSession live = new MovementDemo.MovementDemoSession(new View(), 4, .125f, true);
            live.RequestAttack(); live.AdvanceTime(.125f);
            live.CaptureAxes(1, 0); live.AdvanceTime(.5f);
            ReplayPlayback replay = new ReplayPlayback(live.CaptureReplay());
            ulong liveTick = live.TickNumber;
            live.CaptureAxes(-1, 0); // Live input cannot enter the separate replay.
            replay.Play(); replay.AdvanceTime(2);
            Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
            Assert.That(replay.Observe().Actors[0].X, Is.EqualTo(live.CurrentPosition.X));
            Assert.That(live.TickNumber, Is.EqualTo(liveTick));
        }

        private static GameplaySession Record()
        {
            GameplaySession session = new GameplaySession();
            session.Start(new GameplayScenario(tickDelta: .125f));
            session.Submit(new GameplayRequest(session.Id, 1, 1, GameplayActionKind.Attack, 1, 2));
            session.Submit(new GameplayRequest(session.Id, 2, 2, GameplayActionKind.Attack, 1, 2));
            session.Submit(new GameplayRequest(session.Id, 3, 3, GameplayActionKind.Attack, 1, 2));
            session.Submit(new GameplayRequest(session.Id, 4, 3, GameplayActionKind.Move, 2, x: 1));
            session.Submit(new GameplayRequest(session.Id, 5, 4, GameplayActionKind.Move, 1, x: 1));
            for (int i = 0; i < 16; i++) session.Step();
            return session;
        }

        [TestCase(1f / 30)]
        [TestCase(1f / 60)]
        [TestCase(1f / 144)]
        [TestCase(.37f)]
        public void ReplayMatchesAcrossFrameRatesIncludingDeathAndInputFreeTail(float delta)
        {
            GameplaySession original = Record();
            ReplayPlayback replay = new ReplayPlayback(original.CaptureReplay());
            replay.Play();
            for (int i = 0; i < 10000 && replay.State == ReplayPlaybackState.Playing; i++) replay.AdvanceTime(delta);
            Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
            Assert.That(replay.FirstDifference, Is.Null);
            Assert.That(replay.Observe().Tick, Is.EqualTo(16));
            Assert.That(replay.Observe().Actors[1].Active, Is.False);
            Assert.That(replay.Observe().Actors[0].X, Is.EqualTo(original.Observe().Actors[0].X));
            Assert.That(replay.PresentationAlpha, Is.EqualTo(1));
        }

        [Test]
        public void PauseStepRestartAndEndDoNotAdvanceUnexpectedly()
        {
            ReplayPlayback replay = new ReplayPlayback(Record().CaptureReplay());
            replay.AdvanceTime(1);
            Assert.That(replay.Observe().Tick, Is.Zero);
            replay.Step();
            Assert.That(replay.Observe().Tick, Is.EqualTo(1));
            replay.Play(); replay.AdvanceTime(.25f); replay.Pause();
            ulong tick = replay.Observe().Tick;
            replay.AdvanceTime(10);
            Assert.That(replay.Observe().Tick, Is.EqualTo(tick));
            replay.Restart();
            Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Paused));
            Assert.That(replay.Observe().Tick, Is.Zero);
            replay.Play(); replay.AdvanceTime(10);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(replay.Observe().Tick, Is.EqualTo(16));
            Assert.Throws<InvalidOperationException>(() => replay.Step());
        }

        [Test]
        public void SnapshotIsIndependentOfLaterRecordingAndPendingInputs()
        {
            GameplaySession original = Record();
            original.Submit(new GameplayRequest(original.Id, 6, 30, GameplayActionKind.Move, 1));
            ReplayArtifact saved = original.CaptureReplay();
            original.Step(); original.Reset(new GameplayScenario());
            ReplayPlayback replay = new ReplayPlayback(saved);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(saved.EndTick, Is.EqualTo(16));
            Assert.That(saved.Actions.Count, Is.EqualTo(6));
            Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
        }

        [Test]
        public void ZeroTickRecordingIsValidAndFaultCaptureIsRejected()
        {
            GameplaySession original = new GameplaySession();
            Assert.Throws<InvalidOperationException>(() => original.CaptureReplay());
            original.Start(new GameplayScenario());
            Assert.That(new ReplayPlayback(original.CaptureReplay()).State, Is.EqualTo(ReplayPlaybackState.Completed));
            original.Reset(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            original.Submit(new GameplayRequest(original.Id, 1, 1, GameplayActionKind.Move, 1, x: 1)); original.Step();
            Assert.Throws<InvalidOperationException>(() => original.CaptureReplay());
        }

        [Test]
        public void SaveLoadRoundTripAndNoOverwrite()
        {
            string path = Path.Combine(Path.GetTempPath(), "replay-test-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ReplayArtifact saved = Record().CaptureReplay();
                ReplayFile.SaveNew(path, saved);
                Assert.Throws<IOException>(() => ReplayFile.SaveNew(path, saved));
                ReplayPlayback replay = new ReplayPlayback(ReplayFile.Load(path));
                replay.Play(); replay.AdvanceTime(10);
                Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Completed));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void FirstChangedCheckpointStopsAtExactTick()
        {
            ReplayArtifact saved = Record().CaptureReplay();
            HashCheckpoint[] hashes = saved.Hashes.ToArray();
            hashes[5] = new HashCheckpoint(5, "different");
            ReplayArtifact changed = new ReplayArtifact(saved.Scenario, saved.DiagnosticPolicy, saved.EndTick, saved.Actions, saved.Results, hashes);
            ReplayPlayback replay = new ReplayPlayback(changed);
            replay.Play(); replay.AdvanceTime(10);
            Assert.That(replay.State, Is.EqualTo(ReplayPlaybackState.Diverged));
            Assert.That(replay.FirstDifference.Tick, Is.EqualTo(5));
            Assert.That(replay.FirstDifference.Category, Is.EqualTo("state_hash"));
            Assert.That(replay.Observe().Tick, Is.EqualTo(5));
        }

        [Test]
        public void ResultAndPolicyDifferencesAreReported()
        {
            ReplayArtifact saved = Record().CaptureReplay();
            ActionResult[] results = saved.Results.ToArray();
            results[0] = new ActionResult(1, 1, ActionStatus.Rejected, "changed");
            ReplayPlayback replay = new ReplayPlayback(new ReplayArtifact(saved.Scenario, saved.DiagnosticPolicy, saved.EndTick, saved.Actions, results, saved.Hashes));
            replay.Step();
            Assert.That(replay.FirstDifference.Category, Is.EqualTo("action_result"));
            ReplayPlayback policy = new ReplayPlayback(saved, () => new GameplaySession(policyRevision: "different"));
            Assert.That(policy.FirstDifference.Category, Is.EqualTo("policy"));
            Assert.That(policy.Observe().Tick, Is.Zero);
        }

        [Test]
        public void MalformedAndIncompleteHistoryCannotPlay()
        {
            ReplayArtifact saved = Record().CaptureReplay();
            Assert.Throws<ArgumentException>(() => new ReplayArtifact(saved.Scenario, saved.DiagnosticPolicy, saved.EndTick, saved.Actions, saved.Results, saved.Hashes.Skip(1)));
            Assert.Throws<ArgumentException>(() => new ReplayArtifact(saved.Scenario, saved.DiagnosticPolicy, saved.EndTick, saved.Actions, saved.Results.Skip(1), saved.Hashes));
            Assert.Throws<InvalidOperationException>(() => new ReplayPlayback(saved, () => new GameplaySession(SimulationDriveMode.Realtime)));
        }
    }
}
