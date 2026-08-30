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
