# Replay and Simulation

以純 C# domain 與固定 tick simulation 為核心的 Unity 遊戲架構參考專案。目標是讓玩家操作、測試與 Replay 共用正式玩法路徑。

目前 Demo 主線為 `MovementDemoSession → GameplayDefinition → TestableSimulationSession`；`GameplaySession` 已改為同一 template runtime 的相容 facade，供既有工具與舊格式使用，不再另外保存一套玩法／排程實作。其餘驗收與暫緩項目見下方進度清單。

## 開始閱讀

- **[CharacterMovement 累積教學路線](docs/framework-guide/learning-path.md)**：五個可獨立執行的 Domain → Application → 固定 tick → Testability → Replay 階段，Unity 連接現有 Demo。
- **[分階段實作進度](docs/implementation-progress.md)**：本輪範圍、驗收與暫緩事項。
- [DDD 遊戲框架開發指引](docs/framework-guide/README.md)：API、接線與既有範例索引。
- [Module 命名對照](docs/module-naming.md)。
- [Unity Movement Demo](Assets/game/movement-demo/README.md)。
- [Gameplay Simulation](Assets/game/gameplay-simulation/README.md)。

## 無 Unity 的逐步教學

```powershell
dotnet run --project tools/gameplay-lessons -- all
```

改用 `domain`、`application`、`simulation`、`testability`、`replay` 或 1–5 可選單一階段。每階段都有斷言、獨立初始化及[逐章操作說明](tools/gameplay-lessons/README.md)，直接引用現有 Domain，不複製另一套遊戲實作。

已有[多物件呈現／Pool 與獨立 Physics sensor adapters](Assets/framework.deterministic-simulation.unity/README.md)；Demo 經 GameplayActorPresentation 映射已提交的 active IDs。最終 Unity 驗收另記於實作進度；上述命令不啟動 Unity，也不代替 Editor／Player 驗收。

## 既有 headless 回歸檢查

```powershell
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

需要 .NET 9 SDK 或相容 SDK。執行入口列於 [FrameworkGuideExamples.cs](tools/gameplay-checks/FrameworkGuideExamples.cs)，包含指南範例與多組 gameplay／Replay 檢查；不是全部 NUnit、Unity 場景或 Player Build 驗證。當次結果另記於實作進度，不沿用歷史測試數字。

## Protocol：Deferred（暫緩）

先穩定 deterministic-simulation、testability、game 接線與教學，再處理 Protocol。現有 [核心](Assets/framework.gameplay-protocol/README.md)與[專案 adapter](Assets/game/gameplay-protocol/README.md)保留相容；本輪不擴充協定、不接 transport，也不以 Protocol 遷移完成作為其他階段的退出條件。

保留相容不代表 Protocol 已完成：目前只有 in-process 邊界，沒有外部 listener；其必要相容入口在暫緩期間不刪除。這也不代表所有舊 runtime 型別已退役。

本專案不宣稱跨平台 bitwise determinism、完整 snapshot restore 或 rollback。
