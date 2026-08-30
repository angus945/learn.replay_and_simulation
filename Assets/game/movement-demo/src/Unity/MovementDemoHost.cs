using CharacterMovement.Domain;
using CharacterMovement.Integration;
using UnityEngine;
using UnityEngine.InputSystem;
using GameplaySimulation;
using DebugOverlay.Unity;

namespace MovementDemo.Unity
{
    public sealed partial class MovementDemoHost : MonoBehaviour
    {
        [SerializeField] private Transform characterView;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform grid;
        [SerializeField, Min(0f)] private float speed = 4f;
        [SerializeField, Min(1)] private int ticksPerSecond = 60;
        private MovementDemoSession session;
        private GameplayActorPresentation actorViews;
        private GameObject viewTemplates;
        private GameplayObservation presentedReplayObservation;
        private GameplayDebugOverlay overlay;
        private System.Exception adapterFailure;
        public System.Exception AdapterFailure => adapterFailure;

        public MovementPosition CurrentPosition
        {
            get
            {
                GameplayObservation observation = replay == null ? session.Observe() : replay.Observe();
                ActorObservation player = observation.FindActor(observation.PlayerId);
                return new MovementPosition(player.X, player.Y);
            }
        }
        public ulong TickNumber => replay == null ? session.TickNumber : replay.Observe().Tick;

        private void Awake()
        {
            if (characterView == null || viewCamera == null || ticksPerSecond < 1)
            {
                Debug.LogError("Movement demo requires a character view, camera and positive tick rate.", this);
                enabled = false;
                return;
            }
            SpriteRenderer playerSprite = characterView.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                viewTemplates = new GameObject("Inactive View Templates");
                viewTemplates.SetActive(false);
                GameObject playerPrefab = CreateViewTemplate(playerSprite, playerSprite.color, "Player View");
                GameObject enemyPrefab = CreateViewTemplate(playerSprite, new Color(1f, .35f, .3f), "Enemy View");
                actorViews = new GameplayActorPresentation(playerPrefab, enemyPrefab, 1f / ticksPerSecond);
                playerSprite.enabled = false; // The original transform remains the camera anchor.
            }
            session = new MovementDemoSession(new TransformView(characterView), speed, 1f / ticksPerSecond, includeEnemy: true,
                respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40, randomRespawnDelay: true,
                captureObservation: snapshot => actorViews?.Capture(snapshot));
            actorViews?.Capture(session.Observe());
            if (Debug.isDebugBuild)
            {
                overlay = new GameObject("Read Only Diagnostics Overlay").AddComponent<GameplayDebugOverlay>();
                overlay.Bind(session.Diagnostics);
            }
        }

        private GameObject CreateViewTemplate(SpriteRenderer source, Color color, string name)
        {
            GameObject template = new GameObject(name);
            template.transform.SetParent(viewTemplates.transform, false);
            template.transform.localScale = characterView.localScale;
            SpriteRenderer renderer = template.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite; renderer.color = color;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            return template;
        }

        private void Update()
        {
            if (adapterFailure != null) return;
            try
            {
                if (replay != null) { replay.AdvanceTime(Time.deltaTime); return; }
                KeyboardMovementInput.Capture(Keyboard.current, Application.isFocused && !editingReplayPath, session);
                session.AdvanceTime(Time.deltaTime);
            }
            catch (System.Exception error) { StopAdapter(error); }
        }

        private void LateUpdate()
        {
            if (adapterFailure != null) return;
            try { PresentFrame(); }
            catch (System.Exception error) { StopAdapter(error); }
        }

        private void StopAdapter(System.Exception error)
        {
            if (adapterFailure != null) return;
            adapterFailure = error;
            session?.ClearInput();
            Debug.LogException(error, this);
        }

        private void PresentFrame()
        {
            if (replay == null)
            {
                session.UpdatePresentation();
                actorViews?.Render(session.PresentationAlpha);
            }
            else
            {
                GameplayObservation prior = replay.PreviousObservation;
                GameplayObservation next = replay.Observe();
                ActorObservation before = prior.FindActor(prior.PlayerId);
                ActorObservation after = next.FindActor(next.PlayerId);
                characterView.position = Vector3.Lerp(new Vector3(before.X, before.Y, 0), new Vector3(after.X, after.Y, 0), replay.PresentationAlpha);
                if (!ReferenceEquals(presentedReplayObservation, next))
                {
                    actorViews?.Capture(prior);
                    actorViews?.Capture(next);
                    presentedReplayObservation = next;
                }
                actorViews?.Render(replay.PresentationAlpha);
            }
            Vector3 position = characterView.position;
            viewCamera.transform.position = new Vector3(position.x, position.y, -10f);
            if (grid != null)
                grid.position = new Vector3(Mathf.Floor(position.x), Mathf.Floor(position.y), 0f);
        }

        private void OnDisable()
        {
            session?.CaptureAxes(0f, 0f);
            session?.CaptureAttackButton(false);
        }

        private void OnGUI()
        {
            if (session != null) DrawReplayControls();
            if (adapterFailure != null)
            {
                GUI.Box(new Rect(16, 16, 620, 72), "Input / presentation stopped: " + adapterFailure.Message + "\nSave completed ticks, then restart Play Mode. See Console for adapter failure.");
                return;
            }
            if (replay != null) return;
            if (session == null || (overlay != null && overlay.IsVisible)) return;
            GUI.Box(new Rect(16, 16, 440, 140), GUIContent.none);
            GUI.Label(new Rect(30, 25, 370, 25), "CHARACTER MOVEMENT / FIXED TICK");
            GUI.Label(new Rect(30, 50, 420, 25), "WASD / Arrows: move | Space: attack nearby enemy");
            GUI.Label(new Rect(30, 75, 370, 25), $"Tick {session.TickNumber} @ {ticksPerSecond} Hz | alpha {session.PresentationAlpha:F2}");
            GUI.Label(new Rect(30, 98, 370, 25), $"Domain position: {session.CurrentPosition.X:F2}, {session.CurrentPosition.Y:F2}");
            GameplayObservation observation = session.Observe();
            foreach (ActorObservation enemy in observation.Actors)
                if (enemy.Id != observation.PlayerId && enemy.Active)
                { GUI.Label(new Rect(30, 123, 420, 25), $"Enemy #{enemy.Id} HP: {enemy.Health}/{enemy.MaxHealth} | Session: {session.State}"); break; }
        }

        private void OnDestroy()
        {
            replay?.Dispose();
            session?.Dispose();
            actorViews?.Dispose();
            if (viewTemplates != null) Destroy(viewTemplates);
            if (overlay != null) Destroy(overlay.gameObject);
        }

        private sealed class TransformView : ICharacterMovementView
        {
            private readonly Transform target;
            public TransformView(Transform target) => this.target = target;
            public void SetPosition(MovementPosition position)
                => target.position = new Vector3(position.X, position.Y, 0f);
        }
    }
}
