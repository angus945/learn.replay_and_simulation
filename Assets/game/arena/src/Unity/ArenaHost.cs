using System;
using Arena.Composition;
using Arena.Integration;
using Testability;
using Testability.Templates;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arena.Unity
{
    /// <summary>Unity is an outer host: acquire controls, drive one session, then render detached observations.</summary>
    public sealed partial class ArenaHost : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private Transform referenceGrid;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1)] private int ticksPerSecond = 60;
        [SerializeField, Min(1)] private int enemyViewCapacity = 16;
        private ArenaLiveSession live;
        private ArenaActorPresentation views;
        private ArenaDiagnosticsPanel diagnostics;
        private ArenaScenario scenario;
        private Exception adapterFailure;
        private bool livePaused;
        private bool pathFocused;
        private bool disposed;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle statStyle;
        private GUIStyle actorStyle;

        public bool IsInitialized => live != null && !disposed;
        public bool IsReplaying => replay != null;
        public bool IsLivePaused => livePaused;
        public ulong TickNumber => CurrentObservation?.Tick ?? 0;
        public ulong LiveTickNumber => live == null ? 0 : live.TickNumber;
        public ArenaObservation CurrentObservation => replay == null ? live?.Observe() : replay.Observe();
        public Exception AdapterFailure => adapterFailure;
        public ArenaActorPresentation Views => views;
        public ArenaDiagnosticsPanel DiagnosticsPanel => diagnostics;

        private void Awake()
        {
            if (arenaCamera != null && playerPrefab != null && enemyPrefab != null)
                Initialize(arenaCamera, referenceGrid, playerPrefab, enemyPrefab, new ArenaScenario(tickDelta: 1f / ticksPerSecond));
        }

        /// <summary>Also used by integration tests; all gameplay still enters ArenaLiveSession's Submit adapter.</summary>
        public void Initialize(Camera camera, Transform grid, GameObject player, GameObject enemy, ArenaScenario settings = null)
        {
            if (live != null || disposed) throw new InvalidOperationException("An Arena host owns exactly one live session.");
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            arenaCamera = camera; referenceGrid = grid; playerPrefab = player; enemyPrefab = enemy;
            scenario = settings ?? new ArenaScenario();
            ArenaLiveSession next = new ArenaLiveSession(scenario);
            try
            {
                views = new ArenaActorPresentation(playerPrefab, enemyPrefab, scenario.TickDelta, enemyViewCapacity);
                views.Snap(next.Observe());
                live = next;
                BindDiagnostics(live.Diagnostics);
            }
            catch
            {
                views?.Dispose(); views = null; next.Dispose();
                throw;
            }
        }

        public void CaptureControls(float horizontal, float vertical, bool attackDown)
        {
            EnsureInitialized();
            if (replay != null || livePaused) { live.ClearInput(); return; }
            live.CaptureAxes(horizontal, vertical);
            live.CaptureAttack(attackDown);
        }

        public void AdvanceFrame(float seconds)
        {
            EnsureInitialized();
            if (replay == null) live.AdvanceTime(seconds);
            else replay.AdvanceTime(seconds);
        }

        public void RenderFrame()
        {
            EnsureInitialized();
            ArenaObservation current = CurrentObservation;
            ArenaObservation previous;
            float alpha;
            if (replay == null)
            {
                live.UpdatePresentation();
                previous = live.PreviousObservation;
                alpha = livePaused ? 1 : live.PresentationAlpha;
            }
            else
            {
                previous = replay.PreviousObservation;
                alpha = replay.PresentationAlpha;
            }
            views.Present(previous, current, alpha);
            ActorSnapshot player = current.FindActor(current.PlayerId);
            if (player != null)
            {
                Vector3 anchor = views.TryGetView(player.Id, out GameObject playerView)
                    ? playerView.transform.position : new Vector3(player.X, player.Y, 0);
                arenaCamera.transform.position = new Vector3(anchor.x + .65f, anchor.y + .4f, -10);
                if (referenceGrid != null)
                    referenceGrid.position = new Vector3(Mathf.Floor(player.X), Mathf.Floor(player.Y), 0);
            }
            float panelPixels = 372 * UiScale;
            arenaCamera.rect = new Rect(0, 0, Mathf.Clamp01((Screen.width - panelPixels) / Screen.width), 1);
            diagnostics.Refresh(Time.unscaledTime);
        }

        public void PauseLive()
        {
            EnsureInitialized();
            if (replay != null) return;
            live.ClearInput(); live.Pause(); livePaused = true;
            views.Snap(live.Observe());
        }

        public void ResumeLive()
        {
            EnsureInitialized();
            if (replay != null) return;
            live.ClearInput(); live.Resume(); livePaused = false;
        }

        private void Update()
        {
            if (!IsInitialized || adapterFailure != null) return;
            try
            {
                if (replay == null)
                {
                    Keyboard keyboard = Keyboard.current;
                    bool canRead = keyboard != null && UnityEngine.Application.isFocused && !pathFocused && !livePaused;
                    float horizontal = canRead ? Axis(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                        keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) : 0;
                    float vertical = canRead ? Axis(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                        keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) : 0;
                    if (canRead) CaptureControls(horizontal, vertical, keyboard.spaceKey.isPressed);
                    else live.ClearInput();
                }
                AdvanceFrame(Time.deltaTime);
            }
            catch (Exception exception) { FailAdapter(exception); }
        }

        private void LateUpdate()
        {
            if (!IsInitialized || adapterFailure != null) return;
            try { RenderFrame(); }
            catch (Exception exception) { FailAdapter(exception); }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && IsInitialized) live.ClearInput();
        }

        private void OnDisable()
        {
            if (IsInitialized) live.ClearInput();
        }

        private void OnDestroy() => DisposeSessions();

        public void DisposeSessions()
        {
            if (disposed) return;
            disposed = true;
            try { replay?.Dispose(); }
            finally
            {
                try { live?.Dispose(); }
                finally { views?.Dispose(); }
            }
        }

        private void BindDiagnostics(IDiagnosticReader<ArenaObservation> reader)
        {
            diagnostics = new ArenaDiagnosticsPanel(reader);
            diagnostics.Poll();
        }

        private void FailAdapter(Exception exception)
        {
            if (adapterFailure != null) return;
            adapterFailure = exception;
            live?.ClearInput();
            Debug.LogException(exception, this);
        }

        private void EnsureInitialized()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ArenaHost));
            if (live == null) throw new InvalidOperationException("Initialize the Arena host first.");
        }

        private static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
        private static float UiScale => Mathf.Clamp(Screen.width / 1280f, .6f, 1.5f);

        private void OnGUI()
        {
            if (!IsInitialized) return;
            EnsureStyles();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float scale = UiScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float canvasWidth = width - 372;
            DrawHeading(canvasWidth);
            DrawActorLabels(scale, height);
            DrawReplayControls(new Rect(24, height - 188, canvasWidth - 48, 164));
            diagnostics.Draw(new Rect(width - 356, 16, 340, height - 32));
            if (adapterFailure != null)
            {
                ArenaGui.Fill(new Rect(24, 172, canvasWidth - 48, 92), new Color(.27f, .075f, .07f, .98f));
                GUI.Label(new Rect(38, 182, canvasWidth - 76, 70), "HOST ADAPTER STOPPED\n" + adapterFailure.Message +
                    "\nCompleted ticks remain available for saving. Restart Play Mode after fixing the adapter.", subtitleStyle);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawHeading(float canvasWidth)
        {
            ArenaGui.Fill(new Rect(24, 24, canvasWidth - 48, 130), ArenaGui.Panel);
            ArenaGui.Fill(new Rect(24, 24, 4, 130), IsReplaying ? ArenaGui.Coral : ArenaGui.Cyan);
            GUI.Label(new Rect(42, 34, canvasWidth - 80, 38), "ARENA / REPLAY LAB", titleStyle);
            GUI.Label(new Rect(44, 77, canvasWidth - 82, 22), "ONE DOMAIN  /  ONE SESSION  /  REPRODUCIBLE EVIDENCE", subtitleStyle);
            GUI.Label(new Rect(44, 107, canvasWidth - 82, 25),
                "WASD / ARROWS  move     SPACE  attack nearby enemy     TICK " + TickNumber.ToString("D6") +
                "  /  " + (1f / CurrentObservation.TickDelta).ToString("0") + " Hz", statStyle);
        }

        private void DrawActorLabels(float scale, float logicalHeight)
        {
            ArenaObservation observation = CurrentObservation;
            foreach (ActorSnapshot actor in observation.Actors)
            {
                if (!views.TryGetView(actor.Id, out GameObject instance)) continue;
                Vector3 screen = arenaCamera.WorldToScreenPoint(instance.transform.position + new Vector3(0, actor.Enemy ? .6f : -.8f, 0));
                if (screen.z <= 0) continue;
                float x = screen.x / scale;
                float y = logicalHeight - screen.y / scale;
                Color color = actor.Enemy ? ArenaGui.Coral : ArenaGui.Cyan;
                ArenaGui.Fill(new Rect(x - 39, y - 4, 78, 5), new Color(.03f, .045f, .06f, .95f));
                float fraction = actor.MaxHealth > 0 ? Mathf.Clamp01((float)actor.Health / actor.MaxHealth) : 0;
                ArenaGui.Fill(new Rect(x - 38, y - 3, 76 * fraction, 3), color);
                GUI.Label(new Rect(x - 78, y - 24, 156, 22), (actor.Enemy ? "ENEMY" : "PLAYER") + "  " + actor.Health + "/" + actor.MaxHealth, actorStyle);
            }
            if (observation.PendingRespawnTicks.Count > 0)
            {
                float x = (Screen.width / scale - 372) * .5f - 130;
                GUI.Label(new Rect(x, logicalHeight * .68f, 260, 30), "ENEMY RESPAWN SCHEDULED / t" + observation.PendingRespawnTicks[0], actorStyle);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = ArenaGui.Label(27, ArenaGui.Text, FontStyle.Bold);
            subtitleStyle = ArenaGui.Label(11, ArenaGui.Muted);
            statStyle = ArenaGui.Label(12, ArenaGui.Cyan);
            actorStyle = ArenaGui.Label(10, ArenaGui.Text, FontStyle.Bold);
            actorStyle.alignment = TextAnchor.MiddleCenter;
        }
    }
}
