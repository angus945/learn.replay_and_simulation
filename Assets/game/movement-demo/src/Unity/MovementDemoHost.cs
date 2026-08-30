using CharacterMovement.Domain;
using CharacterMovement.Integration;
using UnityEngine;
using UnityEngine.InputSystem;
using GameplaySimulation;
using DebugOverlay.Unity;

namespace MovementDemo.Unity
{
    public sealed class MovementDemoHost : MonoBehaviour
    {
        [SerializeField] private Transform characterView;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform grid;
        [SerializeField, Min(0f)] private float speed = 4f;
        [SerializeField, Min(1)] private int ticksPerSecond = 60;
        private MovementDemoSession session;
        private SpriteRenderer enemyView;
        private GameplayDebugOverlay overlay;

        public MovementPosition CurrentPosition => session.CurrentPosition;
        public ulong TickNumber => session.TickNumber;

        private void Awake()
        {
            if (characterView == null || viewCamera == null || ticksPerSecond < 1)
            {
                Debug.LogError("Movement demo requires a character view, camera and positive tick rate.", this);
                enabled = false;
                return;
            }
            session = new MovementDemoSession(new TransformView(characterView), speed, 1f / ticksPerSecond, includeEnemy: true);
            if (Debug.isDebugBuild)
            {
                overlay = new GameObject("Read Only Diagnostics Overlay").AddComponent<GameplayDebugOverlay>();
                overlay.Bind(session.Diagnostics);
            }
            SpriteRenderer playerSprite = characterView.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                enemyView = new GameObject("Combat Enemy View").AddComponent<SpriteRenderer>();
                enemyView.sprite = playerSprite.sprite;
                enemyView.color = new Color(1f, .35f, .3f);
                enemyView.transform.localScale = characterView.localScale;
            }
        }

        private void Update()
        {
            KeyboardMovementInput.Capture(Keyboard.current, Application.isFocused, session);
            session.AdvanceTime(Time.deltaTime);
        }

        private void LateUpdate()
        {
            session.UpdatePresentation();
            GameplayObservation observation = session.Observe();
            if (enemyView != null && observation.Actors.Count > 1)
            {
                ActorObservation enemy = observation.Actors[1];
                enemyView.enabled = enemy.Active;
                enemyView.transform.position = new Vector3(enemy.X, enemy.Y, 0f);
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
            if (session == null || (overlay != null && overlay.IsVisible)) return;
            GUI.Box(new Rect(16, 16, 440, 140), GUIContent.none);
            GUI.Label(new Rect(30, 25, 370, 25), "CHARACTER MOVEMENT / FIXED TICK");
            GUI.Label(new Rect(30, 50, 420, 25), "WASD / Arrows: move | Space: attack nearby enemy");
            GUI.Label(new Rect(30, 75, 370, 25), $"Tick {session.TickNumber} @ {ticksPerSecond} Hz | alpha {session.PresentationAlpha:F2}");
            GUI.Label(new Rect(30, 98, 370, 25), $"Domain position: {session.CurrentPosition.X:F2}, {session.CurrentPosition.Y:F2}");
            GameplayObservation observation = session.Observe();
            if (observation.Actors.Count > 1)
                GUI.Label(new Rect(30, 123, 420, 25), $"Enemy HP: {observation.Actors[1].Health} | Session: {session.State}");
        }

        private void OnDestroy()
        {
            if (enemyView != null) Destroy(enemyView.gameObject);
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
