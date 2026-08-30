# Arena / Replay Lab

一個完整的 DDD／Clean Architecture Unity 參考應用，展示如何把純 C# 遊戲接上本專案的 deterministic simulation 與 testability frameworks。

從 **[連續教學：Arena guide](docs/arena-guide/README.md)** 開始。十章沿同一份正式程式，從領域規則、接線、生命週期與隨機，一路到診斷、錄製、失敗重現與 Unity。文件以短段落與 list 說明，不需閱讀舊專案歷史。

## 執行

Unity 6000.3.20f1 開啟 `Assets/game/arena/scenes/ArenaDemo.unity`，按 Play。
WASD／方向鍵移動，Space 攻擊附近敵人。畫面提供唯讀 diagnostics、Pause、錄製、載入、逐 tick Replay 與返回原 Live session。

無 Unity 驗證需要 .NET 9 SDK：

```powershell
dotnet run --project tools/arena-checks -- all
dotnet run --project tools/framework-checks
./tools/verify-architecture.ps1
./tools/verify-docs.ps1
```

可以把 `all` 改成 `domain`、`application`、`simulation`、`input`、`lifecycle`、`observation`、`diagnostics`、`replay`、`realtime` 單獨執行章節。

```powershell
dotnet run --project tools/arena-checks -- capture example.json
dotnet run --project tools/arena-checks -- rerun example.json
dotnet run --project tools/arena-checks -- capture-failure failure.json
dotnet run --project tools/arena-checks -- rerun failure.json
```

錄製不覆寫既有檔案。`ReproducedFailure` 表示正確重現教學 oracle 故障，不代表遊戲運作成功。

## 結構與唯一接線

- [Arena game](Assets/game/arena/README.md)：Domain、Application、Infrastructure、Integration、Composition、Unity、Editor。
- [Deterministic simulation](Assets/framework.deterministic-simulation/README.md)：tick、phase、messages、session、realtime runner。
- [Testability](Assets/framework.testability/README.md)：正式輸入、結果、diagnostics、invariants、recording／Replay。
- [Unity adapters](Assets/framework.deterministic-simulation.unity/README.md)：可重用 pool／presentation，以及獨立的可選 sensors。
- [可執行章節](tools/arena-checks/README.md)與[獨立框架檢查](tools/framework-checks/README.md)。

Unity、CLI、整合測試與 Replay 都由 `ArenaDefinition` 建立正式 session。Domain／Application 不引用 framework、module 或 Unity。`tools/arena-build` 將相同 sources 建成獨立 netstandard2.1 assemblies；Unity asmdef 與 headless ProjectReference 由架構檢查比對。

這個示範不宣稱 cross-platform bitwise determinism、snapshot restore、rollback、dynamic physics authority 或網路 transport。[Protocol framework](Assets/framework.gameplay-protocol/README.md) 保留為獨立模組，沒有接入 Arena。

## 文件與驗證

- [功能接線與驗收清單](docs/arena-guide/capabilities.md)。
- [本次驗證紀錄](docs/verification/arena-rebuild-2026-08-30.md)。
- [封存政策](docs/archive/README.md)：舊 game 完全替換；舊教學與原始驗收只作歷史，不是第二條主線。
