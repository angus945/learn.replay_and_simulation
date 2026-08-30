# Deterministic Simulation Framework

從 [Definition／Session 模板](../../docs/framework-guide/definition-template.md)開始：繼承 SimulationDefinition，完成必要接點，框架負責 Build／Seal、Step、Reset、Stop、Dispose 與失敗鎖定。

- `src/API/SimulationDefinition.cs`：五個必填 abstract hooks、選配 observer。
- `src/API/SimulationBuilder.cs`：組裝與必要 handler 宣告。
- `src/API/SimulationSession.cs`：單執行緒、手動 tick 的通用 host。
- `src/Contract/SimulationSessionState.cs`：Running／Stopped／Faulted／Disposed。
- `tests/SessionTemplateContractChecks.cs`：不依賴 Unity／NUnit 的契約檢查，另由 NUnit wrapper 執行。

這是基本 simulation 模板，不自帶本專案的 request 排程、ActionResult、Realtime 控制權、hash、failure artifact 或 Replay。那些能力仍在現有 GameplaySession／testability 整合，未自動移植到此模板。
通用的 testability／Replay 延伸現已在 Framework.Testability 提供，見 [完整模板](../../docs/framework-guide/testability-replay-template.md)；基本 host 保持不依賴它。
Domain 不需要繼承框架型別。現有 SimulationPipeline／SimulationRunner 低階 API 仍保留，既有 Demo 未改接，避免這輪模板工作改變玩法。
