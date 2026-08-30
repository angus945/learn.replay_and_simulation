# Arena / Replay Lab

一個展示 DDD／Clean Architecture 如何接上 deterministic-simulation 與 testability 的完整小遊戲。不是兩套各自可玩的 framework demo：Unity、headless、錄製及 Replay 使用同一個 ArenaDefinition 與相同 Domain/Application。

從 [十章連續教材](../../../docs/arena-guide/README.md) 開始。各章解釋新增接點、依賴方向、呼叫時機、ownership、可執行片段與反例；[能力清單](../../../docs/arena-guide/capabilities.md) 區分已接線範圍與尚未承諾的能力。

## 執行

在 repository 根目錄：

```powershell
dotnet run --project tools/arena-checks -- all
```

可選 `domain`、`application`、`simulation`、`input`、`lifecycle`、`observation`、`diagnostics`、`replay`、`realtime`。命令的成功只代表所執行 checks；不等於全部 NUnit 或 Unity／Player 驗證。

Unity 場景為 [ArenaDemo](scenes/ArenaDemo.unity)。Play 後 WASD／方向鍵移動、Space 攻擊；HUD 提供唯讀證據與錄製／播放控制。場景組裝來源是 [ArenaSceneBuilder](src/Editor/ArenaSceneBuilder.cs)。

## 各層放什麼

- [Domain](src/Domain/)：Actor aggregate、ActorId、Position、不可變 ArenaRules。沒有 Unity、framework 或 registry reference。
- [Application](src/Application/)：Move／Attack 用例、結果與 facts、repository／lifecycle／random ports、出生預算及 tick 排程。只依賴 Domain。
- [Infrastructure](src/Infrastructure/)：ordered repository、SimulationObjectRegistry adapter、seeded random streams。
- [Integration](src/Integration/)：scenario／input／observation、phase 與事件映射、canonical state、post-tick oracle、trace metadata。
- [Composition](src/Composition/)：ArenaDefinition 與 ArenaLiveSession，建立獨立世界、唯一 driver 和 per-session checks。
- [Unity](src/Unity/)：輸入、時間轉交、pool/view、唯讀面板、recording/replay UI；沒有另一套權威遊戲狀態。

各層有獨立 asmdef；[tools/arena-build](../../../tools/arena-build/) 以對應 netstandard2.1 ProjectReference libraries 建置同一 production sources，不靠單一巨型 executable 隱藏依賴方向。

## 遊戲規則

- 玩家從 `(0,0)` 出生，敵人從 `(1,0)` 出生。Move 設持續方向，固定 tick 推進；斜向不加速。
- 攻擊傷害預設 10，距離 2，採本 tick 位移前位置。死亡立即拒絕後續行為，commit 後移出活動清單，不保留 tombstone。
- 敵人血量為 seed 推導的 20–40；health／delay 分別用 stream 1／2。
- 延遲預設 30–90 ticks，60 Hz 下是 .5–1.5 simulation 秒。出生總數最多 12，包含已出生與待出生預約。
- ActorId 不重用；registry slot／view instance 可以重用，但有各自的 generation 語意。

## 正式接線

```text
Input → Gameplay.Submit → framework InputIntent / InputCommand
  → ArenaApplication → Actor
  → facts / reactions → StructuralCommit
  → ArenaObservation → hash / invariants / recording
  → Unity poses / diagnostics
```

Submit 只代表 admission，不代表遊戲已成功。一般業務拒絕回結構化結果；simulation fault 禁止續跑並保留首次證據。snapshot、canonical state 與 PolicyId 由遊戲明確提供，framework 不猜哪些規則影響未來。

正常與失敗都保存 TemplateRecording。Unity 寫入 `persistentDataPath/ArenaRecordings` 的新檔；Replay 重建獨立 session，逐 tick 比較結果／hash／failure，返回 live 不寫回 replay 狀態。

## 邊界與非目標

Arena 不支援舊 game API／recording 相容。內層不依賴 Protocol、network transport 或 DI container。

本遊戲未接 Physics gameplay；Unity framework 的 local sensor adapters 是可選 reference。沒有 dynamic Rigidbody authority、snapshot restore／rollback、任意 seek、process watchdog 或跨平台 bitwise determinism 保證。

測試來源在 [tests](tests/)；實際結果必須來自當次執行。Domain 規則測試、Application ports 測試、兩 framework 契約、Arena headless integration 與 Unity PlayMode／Player 層次不互相替代。
