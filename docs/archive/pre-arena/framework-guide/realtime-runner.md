# 可組裝的即時 Runner

`RealtimeSimulationRunner` 位於 `framework.deterministic-simulation`，不依賴 Unity，也不依賴 Testability。基本 `SimulationSession` 與 `TestableSimulationSession` 都提供 `CreateRealtimeRunner`。

執行路徑：Unity Update → RealtimeSimulationRunner → Session tick（Testability 版本包含錄製／hash／invariant）→ SimulationRunner → world phases。

## 組裝

Runner 採舊版「依職責注入系統」的方式，契約放在 src/Contract/RealtimeRunnerContracts.cs：

- ISimulationTickSource：TickDelta、TickNumber、PrepareTick、AdvanceTick。由 Session 的私有 adapter 提供，遊戲端拿不到繞過驅動權的入口。
- IRealtimeInputSource：AcquireInput(SimulationTick)，在每個 tick 前取得外部輸入。
- IRealtimePresentation：CaptureTickState(ulong)、Render(float)，分離 tick 快照與畫面呈現。

World／Actor／Physics／dispatcher 仍透過 SimulationBuilder 與 Pipeline 的既有 phase 契約組裝，不在即時 Runner 重複排程。時間維持舊版的 AdvanceTime(deltaTime) 入口，不綁 Unity 靜態 Time。

```csharp
// inputSource implements IRealtimeInputSource;
// presentation implements IRealtimePresentation.
RealtimeSimulationRunner runner = session.CreateRealtimeRunner(
    maxTicksPerFrame: 120,
    input: inputSource,
    presentation: presentation);

runner.AdvanceTime(deltaTime); // Unity Update
runner.UpdatePresentation();   // Unity LateUpdate -> presentation.Render(alpha)

runner.Dispose(); // Release authority first; does not dispose the session.
session.Step();   // Manual control is available again.
session.Dispose();
```

基本 SimulationSession 與 TestableSimulationSession 使用相同介面。輸入與 Presentation 可以省略（空操作），用於無輸入／無畫面的 session。需要 pipeline Presentation 時可由 IRealtimePresentation.Render adapter 呼叫基本 session.Render(alpha)。完整可執行整合參考 MovementDemoSession：明確實作這兩個介面，不包回 Func／Action。

## Contract

- 同一 Session 只能有一個即時 Runner；掛上後所有公開 Manual Step 入口（包含 Testability Simulation facade）都拒絕執行。Runner 使用私有 tick 路徑，仍完整執行 Testability 記錄與驗證。
- `Pause()` 清除累積時間但保留所有權；`Resume()` 不追趕暫停期間時間。Pause 不等於 Session.Stop。
- Reset／Session.Dispose 前先 Dispose Runner；Reset 後重新建立 Runner，採用新 tick delta，不沿用舊累積時間。
- `AdvanceTime` 接受有限且非負的秒數，回傳本次執行 tick 數。每次最多推進 maxTicksPerFrame（預設 120）；剩餘 debt 留在 PendingSeconds，下一次 `AdvanceTime(0)` 也能追趕。沒有自動丟 tick 或 background thread。
- AcquireInput 得到即將執行的 tick，允許 Submit／EnqueueIntent 或 Stop；Stop 後不會再執行該 tick。CaptureTickState 得到已嘗試執行的 tick，可以讀 Observation。Testability 發生已記錄的 fault 時 CaptureTickState 仍執行，Observation 可能是較早 tick，請看 Diagnostics.ObservationTick。
- 輸入／Presentation adapter 不可重入 Runner、釋放 Runner、Reset／Dispose Session 或手動 Step。Runner 的操作必須在建立它的 thread；不是跨執行緒排程器。
- 輸入／Presentation adapter 或基本 Session tick 拋出例外：Runner 保留 Failure、停止且不重試，呼叫端收到例外；需 Dispose 後重建。輸入／Presentation adapter 例外不冒充 domain fault，沒有回滾已送輸入或已完成的 tick。
- Testability 本身記錄的 fault、Stop 或 tick budget 結束：停止追趕、保留 Session evidence。Runner 不吞掉或重試失敗 tick。
- PresentationAlpha 為剩餘時間／tick delta，限制在 0～1；暫停或停止後為 0。若希望暫停時顯示最新 snapshot，由 presentation 選 alpha=1，不改權威狀態。

`SimulationDriveOwnership` 是框架 adapter 的共用支援件，讓 Testability 在不反向依賴的前提下遵守相同所有權規則。一般遊戲不需直接建立它，使用 Session factory 即可。

## Demo 與 Replay

`MovementDemoSession` 已改接 IRealtimeInputSource 與 IRealtimePresentation；AdvanceTime／UpdatePresentation／PresentationAlpha 直接委派，不再自行實作 accumulator。銷毀時先釋放 Runner 再釋放 Session。原本的移動、攻擊、RNG 與重播仍由相同 Definition 執行。

Replay 仍是獨立的 manual session，由 `TemplateReplay` 管理播放／逐 tick 比對；本次沒有改寫 Replay 播放時鐘或把即時 Runner 掛到重播世界。即時錄製可由 Replay 重現，兩者不會共用 tick 權限。

## 測試

SessionTemplateContractChecks 的 RealtimeTimingAndOwnership／RealtimeFailuresAndReentry 與 TemplateContractChecks.RealtimeRecordingAndOwnership 同時在純 .NET 與 Unity EditMode 執行，涵蓋時間累積、追趕上限、Pause/Resume、雙重驅動、Reset/rebind、thread/reentry、例外、budget、diagnostics、即時錄製與 Replay。

2026-08-30：純 .NET gameplay-checks 通過、Unity 編譯無錯誤、EditMode 162/162 通過，包含改接後 Demo 的既有移動／攻擊／Replay 測試。本輪未切換 Play Mode 或修改場景資產。
