# Deterministic Simulation Framework

從 [Definition／Session 模板](../../docs/framework-guide/definition-template.md)開始：繼承 SimulationDefinition，完成必要接點，框架負責 Build／Seal、Step、Reset、Stop、Dispose 與失敗鎖定。

- `src/API/SimulationDefinition.cs`：五個必填 abstract hooks、選配 observer。
- `src/API/SimulationBuilder.cs`：組裝與必要 handler 宣告。
- `src/API/SimulationSession.cs`：單執行緒通用 host，支援手動 tick 或組裝即時 Runner。
- `src/API/RealtimeSimulationRunner.cs`：frame delta 累積、bounded catch-up、Pause／Resume 與獨占 tick 驅動權；見 [使用指引](../../docs/framework-guide/realtime-runner.md)。
- `src/Contract/SimulationSessionState.cs`：Running／Stopped／Faulted／Disposed。
- `src/Contract/RealtimeRunnerContracts.cs`：ISimulationTickSource、IRealtimeInputSource、IRealtimePresentation；依職責注入 Runner，不接受 Func／Action 組裝。
- `tests/SessionTemplateContractChecks.cs`：不依賴 Unity／NUnit 的契約檢查，另由 NUnit wrapper 執行。

這是基本 simulation 模板，不自帶本專案的 request 排程、ActionResult、hash、failure artifact 或 Replay。Realtime 控制權由 Session.CreateRealtimeRunner 提供；其餘能力由 Testability 整合。
通用的 testability／Replay 延伸現已在 Framework.Testability 提供，見 [完整模板](../../docs/framework-guide/testability-replay-template.md)；基本 host 保持不依賴它。
Domain 不需要繼承框架型別。SimulationPipeline／SimulationRunner 低階 API 仍保留；Demo 已使用 Definition／Testability Session／RealtimeSimulationRunner 完整路徑。

## 執行契約

- Session 與 Runner 的操作只能由建立它們的執行緒呼叫；外部 transport 必須先排入 owner thread。callback 不能重入 Step、Reset、Dispose 或 driver。
- Participant 依 Composition Root 的註冊順序執行，Seal 後不能新增。每個 PrePhysics／Physics／PostPhysics／StructuralCommit phase 先執行所有 participant，再 drain reactions，不在兩個 participant 之間偷偷派發。
- Reactions 先 drain command waves，再 drain event waves；event 產生 command 時進入下一 reaction cycle。只有 event 也會被派發。Wave number 是單一 dispatcher 的區域序號；wave 與 reaction cycle 都有上限。
- Presentation capture／render 只能觀察並呈現，不應建立 gameplay work；presentation 階段不會 drain reactions。
- `WaveDispatcher` callback 可 Enqueue，但不能遞迴 DispatchAll 或 Clear；未處理的 callback exception 會清除該 dispatcher 的工作。
- 低階 `SimulationRunner.AdvanceTime` 保留原有 void API，每次最多推進 `MaxTicksPerAdvanceTime`（預設 120），剩餘時間保留到後續呼叫。正式 realtime 接線仍優先使用 Session.CreateRealtimeRunner。
- 低階 Runner 的 tick／render exception 會保存第一個 `Failure` 並停止後續執行；`TickNumber` 是嘗試執行的 tick，`LastCompletedTick` 是最後完成的 tick，不代表 rollback。失敗後重建 pipeline 和 runner；使用 Session 時由 Reset 重建。

`tests/CoreHardeningContractChecks.cs` 可由純 C# runner 或 NUnit 執行，涵蓋 bounded catch-up、partial failure、重入、owner thread、多 participant 順序與 reaction 時機。
