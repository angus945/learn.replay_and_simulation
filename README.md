# Replay and Simulation

以純 C# domain 與固定 tick simulation 為核心的 Unity 遊戲架構參考專案。目標是讓玩家操作、測試與 Replay 共用正式玩法路徑。

Demo、工具與測試使用 `GameplayDefinition → TestableSimulationSession`；Unity 經 MovementDemoSession 接入同一份玩法。舊 GameplaySession facade／舊 artifact API 已退役，現行錄製統一為 TemplateRecording。歷史檔案與基準 `22f6966` 的處理方式見 [退休政策](docs/legacy-compatibility-retirement.md)。

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

## Protocol adapter 與暫緩範圍

現行 [專案 adapter](Assets/game/gameplay-protocol/README.md)直接接正式 session ports，game payload 契約為 v2；[Protocol 核心](Assets/framework.gameplay-protocol/README.md) envelope 維持 v1。兩個版本描述不同邊界，不代表舊 game payload 可直接沿用。

**Transport 仍 Deferred（暫緩）**：目前只有 in-process 邊界，沒有外部 listener、HTTP／WebSocket client 或連線管理。adapter 遷移不增加網路服務，也不再需要保留舊 GameplaySession。教學不以協定為前置條件。

本專案不宣稱跨平台 bitwise determinism、完整 snapshot restore 或 rollback。
