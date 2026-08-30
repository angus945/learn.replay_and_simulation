using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Arena.Integration;
using Arena.Unity;
using NUnit.Framework;
using Testability.Templates;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Arena.Tests.PlayMode
{
    public sealed class ArenaRetainedUiTests
    {
        [Test]
        public void PerformanceMetricsPublishCachedSamplesWithoutDependingOnWallClock()
        {
            ArenaPerformanceMetrics metrics = new ArenaPerformanceMetrics();
            metrics.Reset(10, 20);
            string initial = metrics.Summary;
            metrics.Sample(10.25, 22, .01);
            Assert.That(metrics.Summary, Is.SameAs(initial));
            metrics.Sample(10.5, 24, .075);
            Assert.That(metrics.FramesPerSecond, Is.GreaterThan(0));
            Assert.That(metrics.TicksPerSecond, Is.EqualTo(8).Within(.000001));
            Assert.That(metrics.PendingSeconds, Is.EqualTo(.075).Within(.000001));
            string published = metrics.Summary;
            double frames = metrics.FramesPerSecond;
            double ticks = metrics.TicksPerSecond;
            double pending = metrics.PendingSeconds;
            metrics.Sample(10.6, 27, .2);
            Assert.That(metrics.Summary, Is.SameAs(published), "Formatting belongs to the sample interval, not every render frame.");
            Assert.That(metrics.FramesPerSecond, Is.EqualTo(frames));
            Assert.That(metrics.TicksPerSecond, Is.EqualTo(ticks));
            Assert.That(metrics.PendingSeconds, Is.EqualTo(pending));

            metrics.Reset(20, 100);
            metrics.Sample(20.5, 103, 0);
            Assert.That(metrics.TicksPerSecond, Is.EqualTo(6).Within(.000001),
                "Switching live/replay baselines must not turn a tick discontinuity into measured throughput.");
        }

        [UnityTest]
        public IEnumerator RefreshRetainsControlsAndOnlyRealizesABoundedTraceViewport()
        {
            using (TestHost fixture = new TestHost())
            {
                ArenaHost host = fixture.Host;
                VisualElement root = host.Hud.Root;
                ListView list = root.Q<ListView>("trace-list");
                TextField path = root.Q<TextField>("replay-path");
                Button toggle = root.Q<Button>("diagnostics-toggle");
                Assert.That(list, Is.Not.Null);
                Assert.That(list.virtualizationMethod, Is.EqualTo(CollectionVirtualizationMethod.FixedHeight));
                Assert.That(list.fixedItemHeight, Is.GreaterThan(0));
                IList rows = list.itemsSource;
                Assert.That(rows, Is.SameAs(host.DiagnosticsPanel.TraceRows));

                // A deliberately bounded test viewport avoids relying on the Editor's Game view size.
                list.style.height = 126;
                list.style.minHeight = 126;
                list.style.maxHeight = 126;
                list.style.flexGrow = 0;
                list.style.flexShrink = 0;
                host.CaptureControls(1, 0, false);
                host.AdvanceFrame(5);
                host.DiagnosticsPanel.Poll();
                host.RenderFrame();
                fixture.RefreshHud();
                Assert.That(rows.Count, Is.EqualTo(160));
                yield return null;
                yield return null;

                List<Label> realized = list.Query<Label>(className: "trace-row").ToList();
                Assert.That(realized.Count, Is.GreaterThan(0), "The retained list should bind the visible trace rows.");
                Assert.That(realized.Count, Is.LessThan(32), "Virtualization must not create all 160 historical row elements.");
                Assert.That(realized[0].tooltip, Is.Not.Empty, "The bounded row keeps the full evidence in its tooltip.");
                ArenaTraceRow selected = realized[0].userData as ArenaTraceRow;
                Assert.That(selected, Is.Not.Null);
                VisualElement detailPanel = root.Q<VisualElement>("trace-detail-panel");
                Label detail = root.Q<Label>("trace-detail");
                Assert.That(detailPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
                using (ClickEvent click = ClickEvent.GetPooled())
                {
                    click.target = realized[0];
                    realized[0].SendEvent(click);
                }
                yield return null;
                Assert.That(detailPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(detail.text, Is.EqualTo(selected.Detail),
                    "Runtime clicks must reveal the complete evidence, not only the bounded row or an Editor-only tooltip.");
                for (int index = 0; index < 12; index++)
                {
                    host.AdvanceFrame(.125f);
                    host.DiagnosticsPanel.Poll();
                    fixture.RefreshHud();
                }

                Assert.That(host.Hud.Root, Is.SameAs(root));
                Assert.That(root.Q<ListView>("trace-list"), Is.SameAs(list));
                Assert.That(root.Q<TextField>("replay-path"), Is.SameAs(path));
                Assert.That(root.Q<Button>("diagnostics-toggle"), Is.SameAs(toggle));
                Assert.That(list.itemsSource, Is.SameAs(rows));
                Assert.That(rows.Count, Is.LessThanOrEqualTo(160));
                Assert.That(rows.Contains(selected), Is.False, "Enough newer evidence should evict the originally selected history row.");
                Assert.That(detail.text, Is.EqualTo(selected.Detail),
                    "A recycled row must not replace the immutable evidence currently open in the detail panel.");
                Assert.That(detailPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                yield return Activate(root.Q<Button>("trace-detail-close"));
                Assert.That(detailPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
                yield return null;
                Assert.That(list.Query<Label>(className: "trace-row").ToList().Count, Is.LessThan(32));
                Assert.That(host.AdapterFailure, Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DiagnosticsToggleHidesAndReopensTheSameRetainedView()
        {
            using (TestHost fixture = new TestHost())
            {
                ArenaHost host = fixture.Host;
                VisualElement root = host.Hud.Root;
                VisualElement sidebar = root.Q<VisualElement>("diagnostics-panel");
                ListView list = root.Q<ListView>("trace-list");
                IList rows = list.itemsSource;
                fixture.RefreshHud();
                yield return null;
                yield return Activate(root.Q<Button>("diagnostics-toggle"));
                Assert.That(host.Hud.DiagnosticsVisible, Is.False);
                Assert.That(sidebar.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(host.Hud.SidebarWidthFraction, Is.Zero);
                host.AdvanceFrame(.25f);
                fixture.RefreshHud();
                yield return Activate(root.Q<Button>("diagnostics-toggle"));
                Assert.That(host.Hud.DiagnosticsVisible, Is.True);
                Assert.That(sidebar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                host.DiagnosticsPanel.Poll();
                fixture.RefreshHud();
                Assert.That(root.Q<VisualElement>("diagnostics-panel"), Is.SameAs(sidebar));
                Assert.That(root.Q<ListView>("trace-list"), Is.SameAs(list));
                Assert.That(list.itemsSource, Is.SameAs(rows));
                Assert.That(host.DiagnosticsPanel.Snapshot.Tick, Is.EqualTo(host.TickNumber));
                Assert.That(host.TickNumber, Is.EqualTo(2), "Showing evidence must not drive a gameplay tick.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplayPathFocusAndEditingSurviveRetainedRefresh()
        {
            using (TestHost fixture = new TestHost())
            {
                ArenaHost host = fixture.Host;
                TextField path = host.Hud.Root.Q<TextField>("replay-path");
                fixture.RefreshHud();
                yield return null;
                path.Focus();
                yield return null;
                Assert.That(host.Hud.IsTextInputFocused, Is.True);
                path.value = "recording path being edited.json";
                Assert.That(host.ReplayPath, Is.EqualTo(path.value));
                host.ReplayPath = "separate host update.json";
                fixture.RefreshHud();
                Assert.That(host.Hud.IsTextInputFocused, Is.True);
                Assert.That(path.value, Is.EqualTo("recording path being edited.json"),
                    "A diagnostic refresh must not overwrite a focused text editor or recreate its control.");
                host.Hud.Root.Focus();
                yield return null;
                fixture.RefreshHud();
                Assert.That(host.Hud.IsTextInputFocused, Is.False);
                Assert.That(path.value, Is.EqualTo(host.ReplayPath));
                Assert.That(host.TickNumber, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetainedControlsUseTheHostFacadeAndUiErrorsPreserveLiveAndReplay()
        {
            string savedPath = null;
            try
            {
                using (TestHost fixture = new TestHost())
                {
                    ArenaHost host = fixture.Host;
                    VisualElement root = host.Hud.Root;
                    TextField path = root.Q<TextField>("replay-path");
                    fixture.RefreshHud();
                    yield return null;
                    yield return Activate(root.Q<Button>("live-toggle"));
                    Assert.That(host.IsLivePaused, Is.True);
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("live-toggle"));
                    Assert.That(host.IsLivePaused, Is.False);
                    host.CaptureControls(1, 0, false);
                    host.AdvanceFrame(.25f);
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("save-recording"));
                    savedPath = host.ReplayPath;
                    Assert.That(File.Exists(savedPath), Is.True);
                    host.AdvanceFrame(.125f);
                    ArenaObservation live = host.CurrentObservation;
                    string missingPath = Path.Combine(UnityEngine.Application.temporaryCachePath,
                        "arena-ui-missing-" + Guid.NewGuid().ToString("N") + ".json");
                    path.value = missingPath;
                    yield return Activate(root.Q<Button>("load-replay"));
                    Assert.That(host.UiMessage, Does.Contain("FileNotFoundException"));
                    Assert.That(host.IsReplaying, Is.False);
                    Assert.That(host.CurrentObservation, Is.SameAs(live));
                    Assert.That(host.IsLivePaused, Is.False);
                    Assert.That(host.AdapterFailure, Is.Null, "A recoverable UI action error must not stop the simulation adapter.");

                    path.value = savedPath;
                    yield return Activate(root.Q<Button>("load-replay"));
                    fixture.RefreshHud();
                    Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                    Assert.That(root.Q<Button>("save-recording").enabledSelf, Is.False);
                    Assert.That(root.Q<Button>("live-toggle").enabledSelf, Is.False);
                    ArenaObservation replay = host.CurrentObservation;
                    path.value = missingPath;
                    yield return Activate(root.Q<Button>("load-replay"));
                    Assert.That(host.CurrentObservation, Is.SameAs(replay));
                    Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                    Assert.That(host.LiveTickNumber, Is.EqualTo(3));
                    host.InvokeUi(() => host.SaveRecording());
                    Assert.That(host.UiMessage, Does.Contain("InvalidOperationException"));
                    Assert.That(host.CurrentObservation, Is.SameAs(replay));
                    Assert.That(host.AdapterFailure, Is.Null);

                    yield return Activate(root.Q<Button>("replay-step"));
                    Assert.That(host.TickNumber, Is.EqualTo(1));
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("replay-restart"));
                    Assert.That(host.TickNumber, Is.Zero);
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("replay-play"));
                    Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Playing));
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("replay-pause"));
                    Assert.That(host.PlaybackState, Is.EqualTo(TemplateReplayState.Paused));
                    fixture.RefreshHud();
                    yield return Activate(root.Q<Button>("return-live"));
                    fixture.RefreshHud();
                    Assert.That(host.IsReplaying, Is.False);
                    Assert.That(host.IsLivePaused, Is.False);
                    Assert.That(host.TickNumber, Is.EqualTo(3));
                    Assert.That(host.CurrentObservation, Is.SameAs(live));
                    Assert.That(root.Q<Button>("save-recording").enabledSelf, Is.True);
                    Assert.That(root.Q<Button>("replay-play").enabledSelf, Is.False);
                    Assert.That(host.Hud.Root, Is.SameAs(root), "Changing sessions rebinds data without rebuilding the retained UI.");
                }
            }
            finally { if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposalReleasesOwnedDocumentPanelSettingsAndBoundRows()
        {
            UIDocument document;
            PanelSettings ownedSettings;
            PanelSettings sharedSettings = Resources.Load<PanelSettings>("ArenaPanelSettings");
            using (TestHost fixture = new TestHost())
            {
                document = fixture.HostObject.GetComponentInChildren<UIDocument>();
                Assert.That(document, Is.Not.Null);
                ownedSettings = document.panelSettings;
                Assert.That(ownedSettings, Is.Not.SameAs(sharedSettings));
                VisualElement root = fixture.Host.Hud.Root;
                ListView list = root.Q<ListView>("trace-list");
                fixture.Host.DisposeSessions();
                fixture.Host.DisposeSessions();
                Assert.That(root.childCount, Is.Zero);
                Assert.That(list.itemsSource, Is.Null, "Disposed views must stop retaining diagnostic row collections.");
            }
            yield return null;
            Assert.That(document == null, Is.True);
            Assert.That(ownedSettings == null, Is.True);
            Assert.That(sharedSettings == null, Is.False, "A host must destroy its clone, not the shared Resources asset.");
        }

        private static IEnumerator Activate(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledInHierarchy, Is.True);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            // Runtime UI dispatch can finish after the test's current player-loop phase.
            yield return null;
        }

        private sealed class TestHost : IDisposable
        {
            private readonly GameObject player;
            private readonly GameObject enemy;
            private readonly GameObject cameraObject;
            private readonly Camera camera;
            private float refreshTime = 100000;

            internal TestHost()
            {
                player = new GameObject("Arena retained UI test player");
                enemy = new GameObject("Arena retained UI test enemy");
                player.SetActive(false); enemy.SetActive(false);
                cameraObject = new GameObject("Arena retained UI test camera");
                camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.enabled = false;
                HostObject = new GameObject("Arena retained UI test host");
                Host = HostObject.AddComponent<ArenaHost>();
                Host.enabled = false; // Tests provide frames explicitly, independent of keyboard and wall-clock timing.
                try { Host.Initialize(camera, null, player, enemy, new ArenaScenario(tickDelta: .125f, traceCapacity: 512)); }
                catch { Dispose(); throw; }
            }

            internal GameObject HostObject { get; }
            internal ArenaHost Host { get; }

            internal void RefreshHud()
            {
                refreshTime += 1;
                Host.Hud.Refresh(Host.CurrentObservation, Host.Views, camera, refreshTime);
            }

            public void Dispose()
            {
                Host.DisposeSessions();
                Object.Destroy(HostObject); Object.Destroy(cameraObject); Object.Destroy(player); Object.Destroy(enemy);
            }
        }
    }
}
