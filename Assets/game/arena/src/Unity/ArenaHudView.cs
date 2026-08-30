using System;
using System.Collections.Generic;
using System.Globalization;
using Arena.Integration;
using Testability.Templates;
using UnityEngine;
using UnityEngine.UIElements;

namespace Arena.Unity
{
    /// <summary>
    /// Retained presentation only. Diagnostic strings are prepared by a read-only presenter;
    /// the virtualized list binds only visible rows, and controls delegate to the outer host.
    /// </summary>
    public sealed class ArenaHudView : IDisposable
    {
        private readonly ArenaHost host;
        private readonly GameObject owner;
        private readonly PanelSettings settings;
        private readonly VisualElement sidebar;
        private readonly VisualElement actorOverlay;
        private readonly VisualElement heading;
        private readonly Label tickLabel;
        private readonly Label performanceLabel;
        private readonly Label modeLabel;
        private readonly Label messageLabel;
        private readonly Label failureLabel;
        private readonly Label respawnLabel;
        private readonly Label sessionLabel;
        private readonly Label stateLabel;
        private readonly Label invariantLabel;
        private readonly Label observationLabel;
        private readonly Label traceStatusLabel;
        private readonly Label diagnosticErrorLabel;
        private readonly VisualElement traceDetailPanel;
        private readonly Label traceDetail;
        private readonly TextField replayPath;
        private readonly ListView traceList;
        private readonly Button diagnosticsToggle;
        private readonly Button saveRecording;
        private readonly Button liveToggle;
        private readonly Button replayPlay;
        private readonly Button replayPause;
        private readonly Button replayStep;
        private readonly Button replayRestart;
        private readonly Button returnLive;
        private readonly Dictionary<ulong, ActorLabel> actorLabels = new Dictionary<ulong, ActorLabel>();
        private readonly List<ulong> retiredActors = new List<ulong>();
        private readonly Stack<ActorLabel> spareLabels = new Stack<ActorLabel>();
        private ArenaDiagnosticsPanel diagnostics;
        private ArenaObservation actorObservation;
        private int displayedRevision = -1;
        private int displayedTraceRevision = -1;
        private float nextLabels;
        private bool disposed;

        public ArenaHudView(ArenaHost host, ArenaDiagnosticsPanel diagnostics)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            PanelSettings source = Resources.Load<PanelSettings>("ArenaPanelSettings");
            VisualTreeAsset template = Resources.Load<VisualTreeAsset>("ArenaHud");
            if (source == null || template == null)
                throw new InvalidOperationException("Arena UI resources are missing. Run Tools / Arena / Create Demo Scene.");

            settings = UnityEngine.Object.Instantiate(source);
            settings.name = "Arena / owned runtime panel settings";
            owner = new GameObject("Arena / retained HUD");
            owner.transform.SetParent(host.transform, false);
            try
            {
                UIDocument document = owner.AddComponent<UIDocument>();
                document.panelSettings = settings;
                document.visualTreeAsset = template;
                document.sortingOrder = 100;
                Root = document.rootVisualElement;
                Root.style.flexGrow = 1;
                Root.pickingMode = PickingMode.Ignore;
                Root.focusable = true;
                sidebar = Require<VisualElement>("diagnostics-panel");
                actorOverlay = Require<VisualElement>("actor-overlay");
                heading = Require<VisualElement>("heading");
                tickLabel = Require<Label>("tick-label");
                performanceLabel = Require<Label>("performance-label");
                modeLabel = Require<Label>("mode-label");
                messageLabel = Require<Label>("message-label");
                failureLabel = Require<Label>("failure-label");
                respawnLabel = Require<Label>("respawn-label");
                sessionLabel = Require<Label>("session-label");
                stateLabel = Require<Label>("state-label");
                invariantLabel = Require<Label>("invariant-label");
                observationLabel = Require<Label>("observation-label");
                traceStatusLabel = Require<Label>("trace-status-label");
                diagnosticErrorLabel = Require<Label>("diagnostic-error-label");
                traceDetailPanel = Require<VisualElement>("trace-detail-panel");
                traceDetail = Require<Label>("trace-detail");
                Connect("trace-detail-close", () => SetShown(traceDetailPanel, false));
                replayPath = Require<TextField>("replay-path");
                replayPath.RegisterValueChangedCallback(OnPathChanged);
                diagnosticsToggle = Connect("diagnostics-toggle", () => SetDiagnosticsVisible(!DiagnosticsVisible));
                saveRecording = Connect("save-recording", () => host.SaveRecording());
                liveToggle = Connect("live-toggle", () => { if (host.IsLivePaused) host.ResumeLive(); else host.PauseLive(); });
                Connect("load-replay", () => host.LoadReplay(host.ReplayPath));
                replayPlay = Connect("replay-play", host.PlayReplay);
                replayPause = Connect("replay-pause", host.PauseReplay);
                replayStep = Connect("replay-step", host.StepReplay);
                replayRestart = Connect("replay-restart", host.RestartReplay);
                returnLive = Connect("return-live", host.ReturnToLive);

                traceList = Require<ListView>("trace-list");
                traceList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
                traceList.fixedItemHeight = 42;
                traceList.selectionType = SelectionType.None;
                traceList.reorderable = false;
                traceList.makeItem = MakeTraceRow;
                traceList.bindItem = BindTraceRow;
                BindDiagnostics(diagnostics);
                SetDiagnosticsVisible(true);
            }
            catch
            {
                DestroyOwned(owner);
                DestroyOwned(settings);
                throw;
            }
        }

        public VisualElement Root { get; }
        public bool DiagnosticsVisible { get; private set; }

        public bool IsTextInputFocused
        {
            get
            {
                VisualElement focused = Root.panel?.focusController?.focusedElement as VisualElement;
                return focused != null && (ReferenceEquals(focused, replayPath) || replayPath.Contains(focused));
            }
        }

        public float SidebarWidthFraction
        {
            get
            {
                if (!DiagnosticsVisible) return 0;
                float width = Root.resolvedStyle.width;
                float sidebarWidth = sidebar.resolvedStyle.width;
                return width > 0 && !float.IsNaN(sidebarWidth) ? Mathf.Clamp01(sidebarWidth / width) : .31f;
            }
        }

        public void BindDiagnostics(ArenaDiagnosticsPanel panel)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ArenaHudView));
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            diagnostics?.SetVisible(false);
            diagnostics = panel;
            diagnostics.SetVisible(DiagnosticsVisible);
            traceList.itemsSource = diagnostics.TraceRows;
            displayedRevision = -1;
            displayedTraceRevision = -1;
            SetShown(traceDetailPanel, false);
            actorObservation = null;
            nextLabels = 0;
        }

        public void SetDiagnosticsVisible(bool value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ArenaHudView));
            DiagnosticsVisible = value;
            sidebar.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(diagnosticsToggle, value ? "Hide evidence" : "Show evidence");
            diagnostics?.SetVisible(value);
            if (!value) SetShown(traceDetailPanel, false);
            nextLabels = 0;
        }

        public void Refresh(ArenaObservation observation, ArenaActorPresentation views, Camera camera, float realtime)
        {
            if (disposed) return;
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            RefreshActors(observation, views, camera);
            if (realtime < nextLabels) return;
            nextLabels = realtime + .1f;

            SetText(tickLabel, "TICK " + observation.Tick.ToString("D6", CultureInfo.InvariantCulture) +
                "  /  TARGET " + (1f / observation.TickDelta).ToString("0.#", CultureInfo.InvariantCulture) + " Hz");
            SetText(performanceLabel, host.Performance.Summary);
            SetText(modeLabel, host.ReplayStatus);
            SetText(messageLabel, host.UiMessage);
            SetText(failureLabel, host.AdapterFailure == null ? string.Empty :
                "HOST ADAPTER STOPPED\n" + host.AdapterFailure.Message + "\nCompleted ticks can still be saved.");
            SetShown(failureLabel, host.AdapterFailure != null);
            SetText(respawnLabel, observation.PendingRespawnTicks.Count == 0 ? string.Empty :
                "ENEMY RESPAWN SCHEDULED / t" + observation.PendingRespawnTicks[0]);
            heading.EnableInClassList("replay-mode", host.IsReplaying);
            if (!IsTextInputFocused && replayPath.value != host.ReplayPath) replayPath.SetValueWithoutNotify(host.ReplayPath);
            SetText(liveToggle, host.IsLivePaused ? "Resume live" : "Pause live");
            SetEnabled(saveRecording, !host.IsReplaying);
            SetEnabled(liveToggle, !host.IsReplaying);
            SetEnabled(returnLive, host.IsReplaying);
            SetEnabled(replayPlay, host.PlaybackState == TemplateReplayState.Paused);
            SetEnabled(replayStep, host.PlaybackState == TemplateReplayState.Paused);
            SetEnabled(replayPause, host.PlaybackState == TemplateReplayState.Playing);
            SetEnabled(replayRestart, host.IsReplaying);

            if (!DiagnosticsVisible) return;
            if (displayedRevision != diagnostics.Revision)
            {
                SetText(sessionLabel, diagnostics.SessionText);
                SetText(stateLabel, diagnostics.StateText);
                SetText(invariantLabel, diagnostics.InvariantText);
                SetText(observationLabel, diagnostics.ObservationText);
                SetText(traceStatusLabel, diagnostics.TraceStatusText);
                SetText(diagnosticErrorLabel, diagnostics.ErrorText);
                SetShown(diagnosticErrorLabel, !string.IsNullOrEmpty(diagnostics.ErrorText));
                displayedRevision = diagnostics.Revision;
            }
            if (displayedTraceRevision != diagnostics.TraceRevision)
            {
                traceList.RefreshItems();
                displayedTraceRevision = diagnostics.TraceRevision;
            }
        }

        private void RefreshActors(ArenaObservation observation, ArenaActorPresentation views, Camera camera)
        {
            if (Root.panel == null) return;
            if (!ReferenceEquals(actorObservation, observation))
            {
                retiredActors.Clear();
                foreach (KeyValuePair<ulong, ActorLabel> pair in actorLabels)
                    if (observation.FindActor(pair.Key) == null) retiredActors.Add(pair.Key);
                for (int index = 0; index < retiredActors.Count; index++)
                {
                    ActorLabel retired = actorLabels[retiredActors[index]];
                    retired.Container.style.display = DisplayStyle.None;
                    retired.View = null;
                    spareLabels.Push(retired);
                    actorLabels.Remove(retiredActors[index]);
                }
                for (int index = 0; index < observation.Actors.Count; index++)
                {
                    ActorSnapshot actor = observation.Actors[index];
                    if (!actorLabels.TryGetValue(actor.Id, out ActorLabel label))
                    {
                        label = spareLabels.Count == 0 ? CreateActorLabel() : spareLabels.Pop();
                        actorLabels.Add(actor.Id, label);
                        label.Health = -1;
                    }
                    // Resolve once per completed observation, not during every interpolated frame.
                    views.TryGetView(actor.Id, out GameObject actorView);
                    label.View = actorView;
                    bool roleChanged = label.Enemy != actor.Enemy;
                    label.Enemy = actor.Enemy;
                    label.Container.EnableInClassList("enemy-label", actor.Enemy);
                    if (roleChanged || label.Health != actor.Health || label.MaxHealth != actor.MaxHealth)
                    {
                        label.Health = actor.Health;
                        label.MaxHealth = actor.MaxHealth;
                        SetText(label.Text, (actor.Enemy ? "ENEMY" : "PLAYER") + "  " + actor.Health + "/" + actor.MaxHealth);
                        label.Fill.style.width = Length.Percent(actor.MaxHealth > 0 ? 100f * actor.Health / actor.MaxHealth : 0);
                    }
                }
                actorObservation = observation;
            }

            foreach (KeyValuePair<ulong, ActorLabel> pair in actorLabels)
            {
                ActorLabel label = pair.Value;
                if (label.View == null) { SetShown(label.Container, false); continue; }
                Vector3 screen = camera.WorldToScreenPoint(label.View.transform.position + new Vector3(0, label.Enemy ? .6f : -.8f, 0));
                bool visible = screen.z > 0 && camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
                SetShown(label.Container, visible);
                if (!visible) continue;
                // WorldToScreenPoint is bottom-left based; runtime panel coordinates start at the top-left.
                Vector2 point = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(screen.x, Screen.height - screen.y));
                label.Container.style.translate = new Translate(point.x - 78, point.y - 24);
            }
        }

        private ActorLabel CreateActorLabel()
        {
            VisualElement container = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            container.AddToClassList("actor-label");
            Label text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("actor-label-text");
            VisualElement health = new VisualElement { pickingMode = PickingMode.Ignore };
            health.AddToClassList("actor-health");
            VisualElement fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("actor-health-fill");
            health.Add(fill);
            container.Add(text);
            container.Add(health);
            actorOverlay.Add(container);
            return new ActorLabel(container, text, fill);
        }

        private VisualElement MakeTraceRow()
        {
            Label row = new Label { pickingMode = PickingMode.Position };
            row.AddToClassList("trace-row");
            row.RegisterCallback<ClickEvent>(OnTraceClicked);
            return row;
        }

        private void BindTraceRow(VisualElement element, int index)
        {
            Label row = (Label)element;
            if (index < 0 || index >= diagnostics.TraceRows.Count) return;
            ArenaTraceRow item = (ArenaTraceRow)diagnostics.TraceRows[index];
            SetText(row, item.Summary);
            if (row.tooltip != item.Detail) row.tooltip = item.Detail;
            row.userData = item;
        }

        private void OnTraceClicked(ClickEvent click)
        {
            if (!(click.currentTarget is Label row) || !(row.userData is ArenaTraceRow item)) return;
            SetText(traceDetail, item.Detail);
            SetShown(traceDetailPanel, true);
            Root.Focus();
        }

        private Button Connect(string name, Action action)
        {
            Button button = Require<Button>(name);
            button.focusable = false;
            button.clicked += () => { host.InvokeUi(action); Root.Focus(); nextLabels = 0; };
            return button;
        }

        private void OnPathChanged(ChangeEvent<string> change) => host.ReplayPath = change.newValue;
        private T Require<T>(string name) where T : VisualElement
            => Root.Q<T>(name) ?? throw new InvalidOperationException("ArenaHud.uxml is missing " + name + ".");
        private static void SetText(TextElement element, string value)
        {
            string next = value ?? string.Empty;
            if (element.text != next) element.text = next;
        }
        private static void SetEnabled(VisualElement element, bool enabled)
        {
            if (element.enabledSelf != enabled) element.SetEnabled(enabled);
        }
        private static void SetShown(VisualElement element, bool shown)
        {
            DisplayStyle display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.value != display) element.style.display = display;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            diagnostics.SetVisible(false);
            replayPath.UnregisterValueChangedCallback(OnPathChanged);
            traceList.itemsSource = null;
            traceList.bindItem = null;
            traceList.makeItem = null;
            Root.Clear();
            actorLabels.Clear();
            spareLabels.Clear();
            DestroyOwned(owner);
            DestroyOwned(settings);
        }

        private static void DestroyOwned(UnityEngine.Object instance)
        {
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
        }

        private sealed class ActorLabel
        {
            internal readonly VisualElement Container;
            internal readonly Label Text;
            internal readonly VisualElement Fill;
            internal GameObject View;
            internal bool Enemy;
            internal int Health = -1;
            internal int MaxHealth;
            internal ActorLabel(VisualElement container, Label text, VisualElement fill)
            { Container = container; Text = text; Fill = fill; }
        }
    }
}
