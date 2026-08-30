using System.Collections;
using System.Collections.Generic;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation.Unity;
using GameplaySimulation;
using MovementDemo.Unity;
using NUnit.Framework;
using Testability.Templates;
using UnityEngine;
using UnityEngine.TestTools;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace MovementDemo.Tests.PlayMode
{
    public sealed class GameplayPresentationTests
    {
        [UnityTest]
        public IEnumerator DetachedSnapshotsMapThreeActorsByIdentityAndReuseViewsAcrossReset()
        {
            HashSet<int> existing = ExistingBindings();
            List<UnityActorBinding> owned = new List<UnityActorBinding>();
            GameObject playerPrefab = null;
            GameObject enemyPrefab = null;
            GameObject poolRoot = null;
            GameplayActorPresentation views = null;
            ModernSession session = null;
            try
            {
                playerPrefab = Prefab("Player test template", new Vector3(2, 3, 1));
                enemyPrefab = Prefab("Enemy test template", new Vector3(4, 5, 1));
                GameplayDefinition definition = new GameplayDefinition();
                GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, includeEnemy: false);
                session = definition.CreateTestSession(scenario);
                views = new GameplayActorPresentation(playerPrefab, enemyPrefab, scenario.TickDelta, enemyCapacity: 2);
                owned = NewBindings(existing);
                Assert.That(owned.Count, Is.EqualTo(3));
                poolRoot = owned[0].transform.parent.gameObject;

                // Player ID is neither the first array entry nor a pool slot. These are view fixtures, not injected domain state.
                GameplayObservation first = Snapshot(0, 17, Actor(900, 9), Actor(17, 1), Actor(4, -4));
                views.Capture(first); views.Render(1);
                Assert.That(views.ActiveCount, Is.EqualTo(3));
                AssertPosition(Binding(owned, 17), 1);
                AssertPosition(Binding(owned, 4), -4);
                AssertPosition(Binding(owned, 900), 9);
                Assert.That(Binding(owned, 17).transform.localScale, Is.EqualTo(playerPrefab.transform.localScale));
                Assert.That(Binding(owned, 4).transform.localScale, Is.EqualTo(enemyPrefab.transform.localScale));
                Assert.That(Binding(owned, 900).transform.localScale, Is.EqualTo(enemyPrefab.transform.localScale));
                UnityActorBinding reused = Binding(owned, 4);
                InstanceHandle previousHandle = reused.Instance;

                // Reordering the immutable snapshot must not swap identities or interpolation histories.
                views.Capture(Snapshot(1, 17, Actor(4, -2), Actor(900, 11), Actor(17, 3)));
                views.Render(.5f);
                AssertPosition(Binding(owned, 4), -3);
                AssertPosition(Binding(owned, 17), 2);
                AssertPosition(Binding(owned, 900), 10);
                Assert.That(first.FindActor(17).X, Is.EqualTo(1), "Presentation must not mutate an earlier snapshot.");

                views.Capture(Snapshot(2, 17, Actor(17, 3), Actor(4, -2, active: false), Actor(900, 11)));
                views.Render(1);
                Assert.That(views.ActiveCount, Is.EqualTo(2));
                Assert.That(reused.IsBound, Is.False);
                Assert.That(reused.gameObject.activeSelf, Is.False);
                views.Capture(Snapshot(3, 17, Actor(72, 20), Actor(900, 11), Actor(17, 3)));
                views.Render(0);
                Assert.That(Binding(owned, 72), Is.SameAs(reused), "Replacement should reuse the available enemy instance.");
                Assert.That(reused.Instance.Slot, Is.EqualTo(previousHandle.Slot));
                Assert.That(reused.Instance.Generation, Is.GreaterThan(previousHandle.Generation));
                AssertPosition(reused, 20); // A new identity snaps; it must not blend from the despawned actor.

                // A session switch may have consecutive displayed tick numbers; explicit Snap must still discard history.
                views.Snap(Snapshot(4, 17, Actor(72, 30), Actor(17, 50), Actor(900, 40)));
                views.Render(0);
                AssertPosition(Binding(owned, 17), 50);
                Assert.That(session.CurrentTick, Is.Zero, "Detached presentation fixtures must not drive the actual session.");
                session.Admin.Reset(scenario);
                GameplayObservation reset = session.Observe();
                views.Snap(reset); views.Render(0);
                Assert.That(views.ActiveCount, Is.EqualTo(1));
                AssertPosition(Binding(owned, reset.PlayerId), 0);
            }
            finally
            {
                session?.Dispose();
                views?.Dispose();
                if (playerPrefab != null) Object.Destroy(playerPrefab);
                if (enemyPrefab != null) Object.Destroy(enemyPrefab);
            }
            yield return null;
            AssertReleased(owned, poolRoot, playerPrefab, enemyPrefab);
        }

        [UnityTest]
        public IEnumerator DemoCaptureDrivesDeathRespawnReplayAndReturnToLiveWithoutAdvancingItsClock()
        {
            HashSet<int> existing = ExistingBindings();
            List<UnityActorBinding> owned = new List<UnityActorBinding>();
            GameObject playerPrefab = null;
            GameObject enemyPrefab = null;
            GameObject anchor = null;
            GameObject poolRoot = null;
            GameplayActorPresentation views = null;
            MovementDemoSession live = null;
            ModernReplay replay = null;
            int captures = 0;
            try
            {
                playerPrefab = Prefab("Live player test template", Vector3.one);
                enemyPrefab = Prefab("Live enemy test template", Vector3.one);
                anchor = new GameObject("Live camera anchor test object");
                views = new GameplayActorPresentation(playerPrefab, enemyPrefab, .125f);
                owned = NewBindings(existing);
                Assert.That(owned.Count, Is.EqualTo(2));
                poolRoot = owned[0].transform.parent.gameObject;
                live = new MovementDemoSession(new AnchorView(anchor.transform), speed: 4, tickDeltaTime: .125f,
                    includeEnemy: true, respawnEnemies: true, enemyHealthMin: 10, enemyHealthMax: 10,
                    captureObservation: snapshot => { captures++; views.Capture(snapshot); });
                GameplayObservation initial = live.Observe();
                ulong player = initial.PlayerId;
                ulong firstEnemy = ActiveEnemy(initial);
                views.Snap(initial);
                UnityActorBinding enemyView = Binding(owned, firstEnemy);
                InstanceHandle firstHandle = enemyView.Instance;

                live.CaptureAxes(1, 0); live.AdvanceTime(.125f);
                views.Render(.5f);
                AssertPosition(Binding(owned, player), .25f);
                Assert.That(captures, Is.EqualTo(1));
                live.CaptureAxes(0, 0); live.RequestAttack(); live.AdvanceTime(.125f);
                views.Render(1);
                GameplayObservation afterDeath = live.Observe();
                ulong replacement = ActiveEnemy(afterDeath);
                Assert.That(replacement, Is.Not.EqualTo(firstEnemy));
                Assert.That(afterDeath.FindActor(firstEnemy).Active, Is.False);
                Assert.That(Binding(owned, replacement), Is.SameAs(enemyView));
                Assert.That(enemyView.Instance.Generation, Is.GreaterThan(firstHandle.Generation));
                Assert.That(views.ActiveCount, Is.EqualTo(2));
                TemplateRecording recording = live.CaptureReplay();

                // Keep a later live tick so returning from replay is a consecutive-tick discontinuity with a different pose.
                live.CaptureAxes(1, 0); live.AdvanceTime(.125f);
                GameplayObservation liveState = live.Observe();
                Assert.That(liveState.Tick, Is.EqualTo(3));
                Assert.That(liveState.FindActor(player).X, Is.EqualTo(1));
                int capturesBeforeReplay = captures;
                replay = new GameplayDefinition().CreateReplay(recording);
                views.Snap(replay.Observe());
                replay.Step();
                views.Capture(replay.PreviousObservation); views.Capture(replay.Observe()); views.Render(.5f);
                AssertPosition(Binding(owned, player), .25f);
                replay.Step();
                views.Capture(replay.PreviousObservation); views.Capture(replay.Observe()); views.Render(1);
                Assert.That(replay.State, Is.EqualTo(TemplateReplayState.Completed));
                Assert.That(ActiveEnemy(replay.Observe()), Is.EqualTo(replacement));
                Assert.That(views.ActiveCount, Is.EqualTo(2));
                Assert.That(live.TickNumber, Is.EqualTo(3), "Replay must not advance the live session.");
                Assert.That(captures, Is.EqualTo(capturesBeforeReplay), "Replay must not call the live capture callback.");

                views.Snap(liveState); views.Render(0);
                AssertPosition(Binding(owned, player), 1, "Returning live must snap even from replay tick 2 to live tick 3.");
                live.ClearInput(); live.AdvanceTime(.125f); views.Render(1);
                Assert.That(live.TickNumber, Is.EqualTo(4));
                AssertPosition(Binding(owned, player), 1);
            }
            finally
            {
                replay?.Dispose();
                live?.Dispose();
                views?.Dispose();
                if (playerPrefab != null) Object.Destroy(playerPrefab);
                if (enemyPrefab != null) Object.Destroy(enemyPrefab);
                if (anchor != null) Object.Destroy(anchor);
            }
            yield return null;
            AssertReleased(owned, poolRoot, playerPrefab, enemyPrefab);
            Assert.That(anchor == null, Is.True, "The test camera anchor was not destroyed.");
        }

        private static GameObject Prefab(string name, Vector3 scale)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            prefab.transform.localScale = scale;
            return prefab;
        }
        private static GameplayObservation Snapshot(ulong tick, ulong player, params ActorObservation[] actors)
            => new GameplayObservation(tick, actors, playerId: player);
        private static ActorObservation Actor(ulong id, float x, bool active = true)
            => new ActorObservation(id, x, 0, 0, 0, 4, active ? 10 : 0, 10, active);
        private static HashSet<int> ExistingBindings()
        {
            HashSet<int> result = new HashSet<int>();
            foreach (UnityActorBinding binding in Object.FindObjectsByType<UnityActorBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                result.Add(binding.GetInstanceID());
            return result;
        }
        private static List<UnityActorBinding> NewBindings(HashSet<int> existing)
        {
            List<UnityActorBinding> result = new List<UnityActorBinding>();
            foreach (UnityActorBinding binding in Object.FindObjectsByType<UnityActorBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!existing.Contains(binding.GetInstanceID())) result.Add(binding);
            return result;
        }
        private static UnityActorBinding Binding(IEnumerable<UnityActorBinding> bindings, ulong id)
        {
            foreach (UnityActorBinding binding in bindings)
                if (binding.IsBound && binding.ObjectId.Value == id) return binding;
            Assert.Fail("No active Unity binding for gameplay actor " + id);
            return null;
        }
        private static ulong ActiveEnemy(GameplayObservation observation)
        {
            foreach (ActorObservation actor in observation.Actors)
                if (actor.Active && actor.Id != observation.PlayerId) return actor.Id;
            Assert.Fail("Expected an active enemy.");
            return 0;
        }
        private static void AssertPosition(UnityActorBinding binding, float x, string message = null)
        {
            Assert.That(binding.gameObject.activeInHierarchy, Is.True);
            Assert.That(binding.transform.position.x, Is.EqualTo(x).Within(.000001f), message);
            Assert.That(binding.transform.position.y, Is.EqualTo(0).Within(.000001f));
        }
        private static void AssertReleased(IEnumerable<UnityActorBinding> bindings, GameObject root, GameObject player, GameObject enemy)
        {
            foreach (UnityActorBinding binding in bindings) Assert.That(binding == null, Is.True, "A pooled Unity view survived Dispose.");
            Assert.That(root == null, Is.True, "The owned actor pool root survived Dispose.");
            Assert.That(player == null && enemy == null, Is.True, "Test templates survived cleanup.");
        }
        private sealed class AnchorView : ICharacterMovementView
        {
            private readonly Transform target;
            internal AnchorView(Transform target) { this.target = target; }
            public void SetPosition(MovementPosition position) => target.position = new Vector3(position.X, position.Y, 0);
        }
    }
}
