# 直接繼承的 Definition／Session 模板

[回到索引](README.md)

首次接線建議先執行 [CharacterMovement 第 3 章](../../tools/gameplay-lessons/lessons/03-simulation.md)，以下說明該章使用的 Definition／Session 接點；沿用同一個 Domain，不需先建立另一個 Player 模型。

這是實際已存在的 API，不是概念草圖。模板位於 `Framework.DeterministicSimulation`，namespace 為 `DeterministicSimulation.Framework`。
需要正式輸入記錄、診斷與重播時，直接使用 [Testability／Replay 延伸模板](testability-replay-template.md)。以下仍描述基本 host，不代表延伸模板缺少這些能力。
你的 Domain 保持原樣；繼承的是外圍組裝 definition。

## 五個必須實作的接點

繼承 `SimulationDefinition<TWorld, TScenario>`（TWorld 必須是 reference type）。未完成以下成員，編譯器會指出缺漏：

| Abstract member | 你提供的責任 |
|---|---|
| ValidateScenario | 驗證專案設定；沒有額外限制也要明確實作空方法 |
| GetTickDelta | 固定 tick 秒數，框架另檢查 finite 且 >0 |
| CreateWorld | 每次建立全新 world／DDD services，不能共用前次可變世界 |
| Configure | 註冊 handlers／participants，宣告必要訊息接線 |
| DestroyWorld | 解除訂閱、釋放資源；純 managed world 可明確 no-op |

TWorld 是你自己的組裝物件，不必實作任何 framework interface；aggregate、repository、domain event 也不必改寫。
Definition 可重用，但不要把 per-session mutable state 保存在 definition，否則多個 session 會互相污染。
若 CreateWorld 在回傳前拋錯，由 CreateWorld 自己清理部分建立的資源，框架無法取得尚未回傳的 world。

## 可以照做的完整範例

閱讀 [MovementDefinitionExample.cs](../../tools/gameplay-checks/MovementDefinitionExample.cs)：

1. MovementWorld 以普通 constructor 組裝既有 Movement domain 與 application。
2. MovementDefinitionExample 繼承模板並填完五個 hooks。
3. Configure 宣告 RequireIntent<PlayerMoveIntent>，再接 handler 與 PrePhysics participant。
4. 實作選配 ISimulationObserver，回傳位置 value object。
5. Verify 示範 CreateSession、EnqueueIntent、Step、Observe、Reset、Dispose。

範例檔在 tools 內，透過下列命令實際編譯、執行；不是要求你在 production 依賴 tools assembly：

```powershell
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

若建立 Unity 遊戲專案，把你的 definition／world 放在 `game/<game>-simulation/src` 的專案 assembly。
引用 Framework.DeterministicSimulation、所使用的 Module.SimulationPrimitives，以及你的 Domain／Application assemblies；不要反向讓 Domain 引用這個組裝 assembly。

## 架構如何指出缺漏？

- 編譯時：abstract hooks、interface 成員、泛型訊息分類不完整會編譯失敗。
- 建立 session 時：RequireIntent／RequireCommand 宣告的 handler 缺漏會一起列出，世界會清理，不產出半成品 session。
- 註冊時：沿用 pipeline 的空 handler、重複唯一 handler／participant 檢查。
- Build 後：builder 封存，不能保留它供 gameplay 動態增加 handler。
- 執行時：未宣告的訊息若缺 handler，仍是派送時錯誤。框架不掃描任意 C# 程式推斷所有可能產生的訊息。

Require 清單是專案的組裝規格；例如 handler 會產生另一個 command，應同時宣告 RequireCommand。
不強迫每個 phase 都有 participant，也不要求 domain event 一定有 subscriber。
目前不支援一般化的「必填 physics／Replay capability」宣告，未註冊可選 participant 不算錯誤。

## Session 的固定流程

CreateSession：Validate → GetTickDelta → CreateWorld → Configure → Build/Seal → Running。
Step：檢查 Running／非重入 → runner.AdvanceTick → 更新 LastCompletedTick。
Stop：停止接受 intent 與 Step，但保留世界供 Observe；沒有 Resume。
Reset：先驗證新 scenario，再釋放舊世界並重新組裝。成功後 tick=0、Failure 清空、舊 queue 丟棄。
Dispose：釋放一次世界，重複 Dispose 不重複清理，即使清理曾拋錯。

Step／Render callback 拋例外：記住第一個 Failure、進入 Faulted、向呼叫端重新拋出；不得繼續 Step，必須 Reset。
失敗 tick 是嘗試的 TickNumber；LastCompletedTick 保留最後成功 tick。不 rollback 已執行的 domain 修改。
Faulted 世界仍可 Observe；若 Reset 組裝失敗且已釋放舊世界，Observe 會明確拒絕。
Reset 新設定驗證失敗不碰舊世界；一旦開始 DestroyWorld，就不承諾回復舊世界。
組裝失敗會清理新世界；若組裝與清理都失敗，以 AggregateException 保留兩個錯誤。

## Observation 與呈現

`ISimulationObserver<TWorld, TObservation>` 是選配 adapter。呼叫 `session.Observe(observer)` 只能在 ticks 之間進行。
Observer 應回傳不可變複本／value object，不改世界、不保留 world 引用。框架能阻止 callback 重入 session，不能阻止任意 domain setter 或可變資料洩漏。
Observer 的例外會傳出但不改 session 狀態，因為 observer 契約是唯讀；不得依賴這點在 observer 內改 gameplay。
`Render(alpha)` 呼叫已註冊 presentation participants，alpha 必須在 0..1；此模板不累積 wall time、不自動產生插值 snapshot。

## 基本 Session、Testability 與相容 facade

本模板提供手動 Step，也可透過 CreateRealtimeRunner 建立具唯一驅動權的即時 driver；掛上 Runner 後不可手動 Step，先 Dispose Runner 才能切回手動控制。詳見 [即時 Runner](realtime-runner.md)。
EnqueueIntent 是放入下一次處理的 intent queue，不是帶 SessionId／Sequence／TargetTick 的 gameplay admission。
Stop 保留不可再執行的 queue，Reset／Dispose 丟棄；沒有 ActionResult 或取消結果查詢。
沒有任意公開 world／pipeline 存取；definition／observer 是可信整合程式，不是安全沙箱。

[ReplayableSimulationDefinition／TestableSimulationSession](testability-replay-template.md)已在基本模板外提供 admission、ActionResult、invariants、trace、recording 與 Replay。第 4–5 章與 Demo 以 [GameplayDefinition](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)接入這層；Definition 建立 GameplayWorld，玩法決策仍由 GameplayActions／Domain 執行。

[GameplaySession](../../Assets/game/gameplay-simulation/src/Runtime/GameplaySession.cs)現為同一 GameplayDefinition → TestableSimulationSession 的相容 facade，保留舊 ports／artifact/hash 投影，沒有另一份玩法或 pipeline。新功能直接使用現行 definition，不在 facade 增加 handlers。

基本 framework 不反向依賴 testability 或 Protocol；Replay 來自已存在的延伸層。不要同時把兩個獨立 host 套在同一個可變 world。Protocol 暫緩，只保留相容 consumer，不是建立基本／可重播 session 的前置條件。

## 契約驗證

[SessionTemplateContractChecks.cs](../../Assets/framework.deterministic-simulation/tests/SessionTemplateContractChecks.cs)以不繼承 framework 的 Counter domain，驗證另一種整合寫法。
五組檢查涵蓋生命周期／queue reset、缺漏一次報出、組裝與清理錯誤、重入與 fault tick、無效 Reset、失敗清理不重複、不同 session 隔離。
這些檢查同時由純 .NET runner 與 NUnit wrapper 共用；新專案也應補自己的 world／observer 語義測試，abstract 成員存在不代表實作必然正確。
