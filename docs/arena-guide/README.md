# Arena：DDD／Clean Architecture 與兩個 framework 的完整接線

這份教材只使用一個 Arena。Domain、手動測試、Unity 即時操作、錄製及 Replay 都執行同一份規則；不在學到下一章時更換另一套遊戲。

目標不是學會呼叫一個已完成的 Demo，而是能回答：這個接點為什麼存在、誰實作它、framework 何時呼叫它，以及新功能應改哪一層。

## 完成品與遊戲範圍

Arena 有一名玩家與依序重生的敵人：

- Move 改變持續方向；固定 tick 才推進位置。斜向不加速，類比輸入保留幅度。
- Attack 在範圍內扣血；同 tick 的攻擊使用位移前位置。死亡立即禁止後續行為，活動成員於 StructuralCommit 移除。
- 敵人血量由 seed 決定；死亡後按 tick 排程重生。血量與延遲各用獨立 RNG stream，出生總數包含待出生預約而有上限。
- Unity 只取樣輸入、轉交時間、顯示 immutable observation。拖動 Transform 不會改變權威位置。
- 同一 scenario 與外部輸入可保存為 JSON，在新 session 比對逐 tick hash、操作結果及首次失敗。

這些規則足以展示 Aggregate、Application ports、外圍 adapters 與兩 framework 的合作，不需要為移動、血量、重生各建立一個假想 bounded context。Arena 是一個小型 bounded context；資料夾和 assembly 是依賴邊界，不等於不同領域。

## 先執行，再依序讀十章

在 repository 根目錄執行，需要 .NET 9 SDK 或能建置 net9.0 的相容 SDK：

```powershell
dotnet run --project tools/arena-checks -- all
```

每個 selector 自行建立乾淨狀態，不需先執行上一章。`all` 的成功只表示接入的 headless checks 成功，不代表 Unity 編譯、NUnit 全套、PlayMode 或 Player build。

1. [Domain：把規則留在 Aggregate](01-domain.md) — `domain`
2. [Application：用例與向內定義的 ports](02-application.md) — `application`
3. [Simulation：建立獨立世界與固定 phase](03-simulation.md) — `simulation`
4. [Input：接正式控制面與操作結果](04-input.md) — `input`
5. [Lifecycle：事件、重生排程與 RNG](05-lifecycle.md) — `lifecycle`
6. [Observation：建立唯讀狀態與 canonical bytes](06-observation.md) — `observation`
7. [Diagnostics：oracle、trace 與首次失敗](07-diagnostics.md) — `diagnostics`
8. [Replay：保存輸入並重新證明結果](08-replay.md) — `replay`
9. [Realtime：只有一個時鐘擁有者](09-realtime.md) — `realtime`
10. [Unity：輸入、畫面、錄製與播放接回同一核心](10-unity.md) — Unity 場景與整合測試

各章是同一 production implementation 的增量解說，不是十份完整 game 副本。第 3 章起直接使用最終的 `ArenaDefinition`；讀者逐章補齊對 hooks 的理解，不另外維護一個低階 host 再把第二個 host 套上去。第 1–2 章則直接測試內層規則，尚未使用 simulation。

各章 selector 的可執行來源是 [ArenaContractChecks](../../Assets/game/arena/tests/Integration/ArenaContractChecks.cs)，由 CLI 與 NUnit wrapper 共用。文中的短片段用來逐步解釋 production 接點，不是另一套需要維護的 game；含 `using` 的示範可放在 console top-level program，或將 `using` 放檔案開頭、其餘放測試方法。類別成員片段會註明所屬類別，不要求把它單獨當成完整檔案編譯。

## 先看懂依賴方向

```text
Unity host ───────────────→ Composition
                               ↓
                           Integration ──→ Infrastructure
                               ↓                 ↓
                           Application ←─────────┘
                               ↓
                             Domain

Composition / Integration → Testability → DeterministicSimulation → modules
Infrastructure → registry / seeded-random modules
Unity presentation → DeterministicSimulation.Unity
```

- Domain：Actor、ActorId、Position、ArenaRules。沒有 Unity、framework、registry、trace 型別。
- Application：ArenaRequest／ArenaResult／ArenaFact、用例、repository／lifecycle／random ports。只引用 Domain。
- Infrastructure：以排序 repository、SimulationObjectRegistry、SplitMix64 實作內層要求的 ports。
- Integration：外部 payload、scenario、observation、phase／訊息 adapters、canonical state、診斷規則。
- Composition：`ArenaDefinition` 選擇並組合以上能力；`ArenaLiveSession` 接 frame input 與唯一 realtime runner。
- Unity：MonoBehaviour、鍵盤、pool／view、唯讀 diagnostics 及播放 UI。

這個方向同時落在 [Unity asmdef](../../Assets/game/arena/src/Application/Game.Arena.Application.asmdef) 與 [.NET ProjectReference](../../tools/arena-build/Game.Arena.Application/Game.Arena.Application.csproj)。`tools/arena-build` 的分層 library 以 netstandard2.1 建置 production sources；CLI 引用組裝工程，不把全部來源壓在同一 assembly 來假裝分層。

## 兩個 framework 各負責什麼

`framework.deterministic-simulation` 提供固定 tick、phase、三類訊息與 reaction drain、session/world 生命週期、唯一 realtime driver。它不決定誰能攻擊、敵人何時出生。

`framework.testability` 延伸上述 session，提供帶 identity／sequence／target tick 的 admission、結果查詢、snapshot／hash／invariant 流程、有界 trace、recording 與 Replay。它不另寫一個 game loop，也不自動猜哪些狀態應入 hash。

`ArenaDefinition` 繼承 `ReplayableSimulationDefinition`，不是讓 Actor 繼承 framework。框架回呼 outer adapters，adapters 再呼叫 Application；這就是把「框架執行流程」與「內層規則」接在一起的地方。

## 教材的驗收標準

閱讀每章時應能完成四件事：

- 指出本章新增接點及其檔案，而不是只找到某個 class 名稱。
- 從外部要求一路追到 Domain，並說出狀態在哪個 tick／phase 改變。
- 解釋正常結果與斷線、非法輸入、失敗時的差異。
- 執行本章檢查，並知道它沒有驗證什麼。

完整能力逐項列於 [能力與驗收清單](capabilities.md)。文中的「預期」是行為規格，不是本次執行報告；不沿用舊 game 的測試數字。整體交付狀態由 repository 的驗證報告記錄。

## 刻意不做的事

- 不保留舊 game 的玩法、API、scenario 或 recording 相容。
- 不把 Protocol、transport、遠端認證或自動探索器當兩 framework 的必備接線。
- 不宣稱跨平台 bitwise determinism、任意 snapshot restore、rollback 或 process watchdog。
- Unity physics sensors 是獨立選配 adapter，Arena 沒有接碰撞傷害、dynamic Rigidbody authority 或 physics outcome recording。參考 [Unity framework](../../Assets/framework.deterministic-simulation.unity/README.md)，不要把 phase 名稱當作已執行 PhysX 的證據。

需要查 API 時再讀 [Simulation reference](../../Assets/framework.deterministic-simulation/README.md)、[Testability reference](../../Assets/framework.testability/README.md) 與使用到的 module README；這些是工具書，不是額外的必修 game 教學。
