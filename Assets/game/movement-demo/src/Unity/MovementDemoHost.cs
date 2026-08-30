using CharacterMovement.Domain;
using CharacterMovement.Integration;
using UnityEngine;
using UnityEngine.InputSystem;

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
            session = new MovementDemoSession(new TransformView(characterView), speed, 1f / ticksPerSecond);
        }

        private void Update()
        {
            KeyboardMovementInput.Capture(Keyboard.current, Application.isFocused, session);
            session.AdvanceTime(Time.deltaTime);
        }

        private void LateUpdate()
        {
            session.UpdatePresentation();
            Vector3 position = characterView.position;
            viewCamera.transform.position = new Vector3(position.x, position.y, -10f);
            if (grid != null)
                grid.position = new Vector3(Mathf.Floor(position.x), Mathf.Floor(position.y), 0f);
        }

        private void OnDisable()
        {
            session?.CaptureAxes(0f, 0f);
        }

        private void OnGUI()
        {
            if (session == null) return;
            GUI.Box(new Rect(16, 16, 390, 110), GUIContent.none);
            GUI.Label(new Rect(30, 25, 370, 25), "CHARACTER MOVEMENT / FIXED TICK");
            GUI.Label(new Rect(30, 50, 370, 25), "WASD / Arrow keys - move (click Game view first)");
            GUI.Label(new Rect(30, 75, 370, 25), $"Tick {session.TickNumber} @ {ticksPerSecond} Hz | alpha {session.PresentationAlpha:F2}");
            GUI.Label(new Rect(30, 98, 370, 25), $"Domain position: {session.CurrentPosition.X:F2}, {session.CurrentPosition.Y:F2}");
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
