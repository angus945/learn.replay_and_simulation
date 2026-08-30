using System.Collections;
using System.IO;
using Arena.Composition;
using Arena.Integration;
using Arena.Unity;
using DeterministicSimulation.Unity;
using NUnit.Framework;
using Testability.Templates;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arena.Tests.PlayMode
{
    public sealed class ArenaPresentationTests
    {
        [UnityTest]
        public IEnumerator CatchupUsesPenultimateCompletedObservationAndRenderingNeverDrivesGameplay()
        {
            GameObject player = Template("Arena test player");
            GameObject enemy = Template("Arena test enemy");
            GameObject ownedView = null;
            try
            {
                ArenaScenario scenario = new ArenaScenario(tickDelta: .125f);
                using (ArenaLiveSession session = new ArenaLiveSession(scenario))
                using (ArenaActorPresentation views = new ArenaActorPresentation(player, enemy, scenario.TickDelta))
                {
                    ArenaObservation initial = session.Observe();
                    views.Snap(initial);
                    session.CaptureAxes(1, 0);
                    session.AdvanceTime(.5625f); // Four ticks and half a tick of presentation debt.
                    ArenaObservation current = session.Observe();
                    Assert.That(current.Tick, Is.EqualTo(4));
                    Assert.That(session.PreviousObservation.Tick, Is.EqualTo(3));
                    Assert.That(session.PreviousObservation.FindActor(current.PlayerId).X, Is.EqualTo(1.5f));
                    views.Present(session.PreviousObservation, current, session.PresentationAlpha);
                    Assert.That(views.TryGetView(current.PlayerId, out ownedView), Is.True);
                    Assert.That(ownedView.transform.position.x, Is.EqualTo(1.75f).Within(.000001f));
                    uint generation = ownedView.GetComponent<UnityActorBinding>().Instance.Generation;
                    for (int index = 0; index < 8; index++)
                        views.Present(session.PreviousObservation, current, .5f);
                    Assert.That(ownedView.GetComponent<UnityActorBinding>().Instance.Generation, Is.EqualTo(generation),
                        "Rendering the same observation must not churn pool bindings.");
                    Assert.That(session.TickNumber, Is.EqualTo(4));
                    Assert.That(initial.FindActor(initial.PlayerId).X, Is.Zero, "The initial observation remains detached.");
                    Assert.That(current.FindActor(current.PlayerId).X, Is.EqualTo(2));
                    views.Snap(initial);
                    views.Present(initial, initial, 0);
                    Assert.That(ownedView.transform.position.x, Is.Zero, "A discontinuity must snap even at alpha zero.");
                    Assert.That(session.TickNumber, Is.EqualTo(4));
                }
            }
            finally { Object.Destroy(player); Object.Destroy(enemy); }
            yield return null;
            Assert.That(ownedView == null, Is.True, "Disposing presentation must release its owned pooled instances.");
        }

        [UnityTest]
        public IEnumerator DeathAndDelayedRespawnReuseAViewWithoutReusingLogicalIdentity()
        {
            GameObject player = Template("Arena lifecycle player");
            GameObject enemy = Template("Arena lifecycle enemy");
            GameObject enemyView = null;
            try
            {
                ArenaScenario scenario = new ArenaScenario(tickDelta: .125f, damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2);
                using (ArenaLiveSession session = new ArenaLiveSession(scenario))
                using (ArenaActorPresentation views = new ArenaActorPresentation(player, enemy, scenario.TickDelta, enemyCapacity: 1))
                {
                    ArenaObservation initial = session.Observe();
                    ulong firstEnemy = Enemy(initial);
                    views.Snap(initial);
                    Assert.That(views.TryGetView(firstEnemy, out enemyView), Is.True);
                    UnityActorBinding binding = enemyView.GetComponent<UnityActorBinding>();
                    uint generation = binding.Instance.Generation;
                    session.CaptureAttack(true);
                    session.AdvanceTime(.125f);
                    views.Present(session.PreviousObservation, session.Observe(), 1);
                    Assert.That(session.Observe().FindActor(firstEnemy), Is.Null);
                    Assert.That(views.ActiveCount, Is.EqualTo(1));
                    Assert.That(binding.IsBound, Is.False);
                    Assert.That(enemyView.activeSelf, Is.False);
                    session.CaptureAttack(false);
                    session.AdvanceTime(.25f);
                    ArenaObservation respawned = session.Observe();
                    ulong nextEnemy = Enemy(respawned);
                    views.Present(session.PreviousObservation, respawned, 0);
                    Assert.That(nextEnemy, Is.Not.EqualTo(firstEnemy));
                    Assert.That(views.TryGetView(nextEnemy, out GameObject replacement), Is.True);
                    Assert.That(replacement, Is.SameAs(enemyView));
                    Assert.That(binding.Instance.Generation, Is.GreaterThan(generation));
                    Assert.That(replacement.transform.position.x, Is.EqualTo(respawned.FindActor(nextEnemy).X),
                        "A new identity snaps to its birth pose; it never interpolates from the retired actor.");
                }
            }
            finally { Object.Destroy(player); Object.Destroy(enemy); }
            yield return null;
            Assert.That(enemyView == null, Is.True);
        }

        [UnityTest]
        public IEnumerator HostSaveLoadPlayStepRestartAndReturnLiveShareProductionComposition()
        {
            GameObject player = Template("Arena host player");
            GameObject enemy = Template("Arena host enemy");
            GameObject cameraObject = new GameObject("Arena host test camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            GameObject hostObject = new GameObject("Arena test host");
            ArenaHost host = hostObject.AddComponent<ArenaHost>();
            host.enabled = false; // Explicit frames below; do not depend on wall clock or physical keyboard state.
            string savedPath = null;
            try
            {
                host.Initialize(camera, null, player, enemy, new ArenaScenario(tickDelta: .125f));
                host.CaptureControls(1, 0, false);
                host.AdvanceFrame(.25f);
                host.RenderFrame();
                savedPath = host.SaveRecording();
                Assert.That(File.Exists(savedPath), Is.True);
                host.AdvanceFrame(.125f);
                host.RenderFrame();
                ArenaObservation live = host.CurrentObservation;
                Assert.That(live.Tick, Is.EqualTo(3));
                host.LoadReplay(savedPath);
                Assert.That(host.IsReplaying, Is.True);
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                Assert.That(host.TickNumber, Is.Zero);
                Assert.That(host.Views.TryGetView(live.PlayerId, out GameObject view), Is.True);
                Assert.That(view.transform.position.x, Is.Zero);
                host.StepReplay();
                Assert.That(host.TickNumber, Is.EqualTo(1));
                Assert.That(view.transform.position.x, Is.EqualTo(.5f));
                host.PlayReplay();
                host.AdvanceFrame(.125f);
                host.RenderFrame();
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Completed));
                Assert.That(host.ReplayDifference, Is.Null);
                Assert.That(host.LiveTickNumber, Is.EqualTo(3), "Replay must not drive the suspended live session.");
                host.RestartReplay();
                Assert.That(host.TickNumber, Is.Zero);
                Assert.That(view.transform.position.x, Is.Zero);
                host.PlayReplay(); host.PauseReplay();
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                host.ReturnToLive();
                host.RenderFrame();
                Assert.That(host.IsReplaying, Is.False);
                Assert.That(host.TickNumber, Is.EqualTo(3));
                Assert.That(view.transform.position.x, Is.EqualTo(live.FindActor(live.PlayerId).X),
                    "Returning to live must discard replay interpolation history.");
                host.PauseLive();
                host.AdvanceFrame(1);
                Assert.That(host.TickNumber, Is.EqualTo(3));
                host.ResumeLive();
                host.AdvanceFrame(.125f);
                Assert.That(host.TickNumber, Is.EqualTo(4));
                Assert.That(host.AdapterFailure, Is.Null);
            }
            finally
            {
                host.DisposeSessions();
                Object.Destroy(hostObject); Object.Destroy(cameraObject); Object.Destroy(player); Object.Destroy(enemy);
                if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath);
            }
            yield return null;
            Assert.That(hostObject == null, Is.True);
        }

        [Test]
        public void DiagnosticsConsumeBoundedTraceWithoutAdvancingOrMutatingRecordedTicks()
        {
            using (ArenaLiveSession session = new ArenaLiveSession(new ArenaScenario(tickDelta: .125f, traceCapacity: 32)))
            {
                ArenaDiagnosticsPanel panel = new ArenaDiagnosticsPanel(session.Diagnostics);
                panel.Poll();
                session.CaptureAxes(1, 0);
                session.AdvanceTime(2.5f);
                TemplateRecording before = session.CaptureRecording();
                string hash = before.Ticks[before.Ticks.Count - 1].Hash;
                for (int index = 0; index < 5; index++) panel.Poll();
                Assert.That(panel.SourceOverwrittenCount, Is.GreaterThan(0));
                Assert.That(panel.MissedCount, Is.GreaterThan(0));
                Assert.That(panel.HistoryCount, Is.LessThanOrEqualTo(160));
                Assert.That(panel.Snapshot.Tick, Is.EqualTo(20));
                Assert.That(session.TickNumber, Is.EqualTo(20));
                TemplateRecording after = session.CaptureRecording();
                Assert.That(after.Ticks.Count, Is.EqualTo(before.Ticks.Count));
                Assert.That(after.Ticks[after.Ticks.Count - 1].Hash, Is.EqualTo(hash));
            }
        }

        [UnityTest]
        public IEnumerator HostAcceptsKnownOraclePolicyReproducesFailureAndRejectsUnknownPolicy()
        {
            ArenaScenario scenario = new ArenaScenario(tickDelta: .125f);
            TemplateRecording recording;
            using (TestableSimulationSession<ArenaRuntime, ArenaScenario, ArenaInput, ArenaObservation> session =
                new ArenaDefinition(failureOracle: true).CreateTestSession(scenario))
            {
                session.Gameplay.Submit(session.Id, 1, 1,
                    new ArenaInput(Arena.Application.ArenaAction.Move, session.Observe().PlayerId, x: 1));
                for (int index = 0; index < 4; index++) session.Step();
                Assert.That(session.Failure, Is.Not.Null);
                recording = session.CaptureRecording();
            }
            string path = Path.Combine(UnityEngine.Application.temporaryCachePath, "arena-oracle-" + System.Guid.NewGuid().ToString("N") + ".json");
            string unknownPath = Path.ChangeExtension(path, ".unknown.json");
            GameObject player = Template("Arena oracle player");
            GameObject enemy = Template("Arena oracle enemy");
            GameObject cameraObject = new GameObject("Arena oracle camera");
            GameObject hostObject = new GameObject("Arena oracle host");
            ArenaHost host = hostObject.AddComponent<ArenaHost>();
            host.enabled = false;
            try
            {
                using (FileStream file = File.Create(path)) TemplateRecordingIO.Write(file, recording);
                TemplateRecording unknown = new TemplateRecording("unknown-policy", recording.Runtime, recording.Scenario,
                    recording.TickDelta, recording.Limits, recording.InitialHash, recording.Inputs, recording.Ticks,
                    recording.Failure, recording.Trace, recording.DroppedTraceEntries);
                using (FileStream file = File.Create(unknownPath)) TemplateRecordingIO.Write(file, unknown);
                host.Initialize(cameraObject.AddComponent<Camera>(), null, player, enemy, scenario);
                host.LoadReplay(path);
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                Assert.Throws<InvalidDataException>(() => host.LoadReplay(unknownPath));
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused), "Rejected policies leave the current replay intact.");
                host.PlayReplay();
                host.AdvanceFrame(1);
                host.RenderFrame();
                Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.ReproducedFailure));
                Assert.That(host.ReplayDifference, Is.Null);
                Assert.That(host.LiveTickNumber, Is.Zero);
            }
            finally
            {
                host.DisposeSessions();
                Object.Destroy(hostObject); Object.Destroy(cameraObject); Object.Destroy(player); Object.Destroy(enemy);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(unknownPath)) File.Delete(unknownPath);
            }
            yield return null;
        }

        private static GameObject Template(string name)
        {
            GameObject template = new GameObject(name);
            template.SetActive(false);
            return template;
        }

        private static ulong Enemy(ArenaObservation observation)
        {
            foreach (ActorSnapshot actor in observation.Actors)
                if (actor.Enemy) return actor.Id;
            Assert.Fail("Expected one active enemy.");
            return 0;
        }
    }
}
