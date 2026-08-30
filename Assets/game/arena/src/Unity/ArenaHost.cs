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
        private bool disposed;
        private ArenaHudView hud;
        private readonly ArenaPerformanceMetrics performance = new ArenaPerformanceMetrics();

        public bool IsInitialized => live != null && !disposed;
        public bool IsReplaying => replay != null;
        public bool IsLivePaused => livePaused;
        public ulong TickNumber => CurrentObservation?.Tick ?? 0;
        public ulong LiveTickNumber => live == null ? 0 : live.TickNumber;
        public ArenaObservation CurrentObservation => replay == null ? live?.Observe() : replay.Observe();
        public Exception AdapterFailure => adapterFailure;
        public ArenaActorPresentation Views => views;
        public ArenaDiagnosticsPanel DiagnosticsPanel => diagnostics;
        public ArenaHudView Hud => hud;
        public ArenaPerformanceMetrics Performance => performance;

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
                hud = new ArenaHudView(this, diagnostics);
                performance.Reset(Time.realtimeSinceStartupAsDouble, TickNumber);
            }
            catch
            {
                hud?.Dispose(); hud = null;
                views?.Dispose(); views = null; next.Dispose(); live = null;
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
            arenaCamera.rect = new Rect(0, 0, 1f - hud.SidebarWidthFraction, 1);
            diagnostics.Refresh(Time.unscaledTime);
            performance.Sample(Time.realtimeSinceStartupAsDouble, current.Tick, replay == null ? live.PendingSeconds : 0);
            hud.Refresh(current, views, arenaCamera, Time.unscaledTime);
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
                    bool canRead = keyboard != null && UnityEngine.Application.isFocused && !hud.IsTextInputFocused && !livePaused;
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
            if (!IsInitialized) return;
            try
            {
                if (adapterFailure == null) RenderFrame();
                else hud.Refresh(CurrentObservation, views, arenaCamera, Time.unscaledTime);
            }
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
                finally
                {
                    try { views?.Dispose(); }
                    finally { hud?.Dispose(); }
                }
            }
        }

        private void BindDiagnostics(IDiagnosticReader<ArenaObservation> reader)
        {
            diagnostics = new ArenaDiagnosticsPanel(reader);
            diagnostics.Poll();
            hud?.BindDiagnostics(diagnostics);
            performance.Reset(Time.realtimeSinceStartupAsDouble, TickNumber);
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
    }
}
