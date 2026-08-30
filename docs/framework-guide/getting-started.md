# 入門：從最小玩法到正式控制面

[回到索引](README.md)

## 先執行範例

在 repository 根目錄，使用 .NET 9 SDK 或相容 SDK：

```powershell
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

不需開啟 Unity。新增的[FrameworkGuideExamples.cs](../../tools/gameplay-checks/FrameworkGuideExamples.cs)會隨此命令編譯並執行，接著跑既有 gameplay／Replay 檢查；任一檢查失敗會以非零 exit code 結束。
以下導讀使用該來源，不另維護一份脫離編譯的完整程式副本。

## 範例 A：最小 Domain → Pipeline

閱讀 `MinimalMovement()`，依序理解：

1. 建立 CharacterId 與 Movement aggregate。領域只知道位置、方向、速度。
2. 建立 repository 並加入角色；這是組裝工作，不是玩家操作 API。
3. 建立 MovementApplication，注入 repository。
4. 建立 SimulationPipeline。
5. 註冊 PlayerMoveIntentHandler 與 MovementPrePhysicsParticipant。
6. Seal，再建立 SimulationRunner（tickDelta=0.25 秒）。
7. 入列方向為 (1,0) 的 PlayerMoveIntent。
8. AdvanceTick：先改方向，再於 PrePhysics 推進位置。

預期：速度 4，第一 tick 的 X=1，第二 tick 的 X=2；沒有再次輸入時，持續方向仍有效。
EnqueueIntent 本身不移動角色，且角色不包含 Unity Transform。

這個範例刻意沒有 registry、RNG、testability facade 或 Replay，展示最少的機制依賴。
它使用既有獨立移動 adapter，Intent handler 直接呼叫 application，沒有需要額外發布的事件。
這不等於完整 Demo 的正式操作路徑；完整路徑見範例 B。

### 新專案需要什麼依賴？

重用框架時，最小核心是 `Module.SimulationPrimitives`、`Module.WaveDispatcher`、`Framework.DeterministicSimulation`。
本範例另使用本 repository 的 CharacterMovement Domain／Application／Integration assemblies，方便展示組裝；新遊戲應以自己的領域替換。
目前 module 位於 Assets，不是可直接填入 UPM manifest 的套件名稱。
搬移到新 Unity 專案時需保留來源及 .meta，並透過 asmdef 指定依賴；勿複製 Library 或 Unity 生成的 csproj。

## 範例 B：正式操作、排序與結果

閱讀 `ControlledMovement()`：

1. 建立 Manual GameplaySession，透過 Admin.Start 初始化。
2. 設定無敵人、tickDelta=0.25，讓觀察只聚焦移動。
3. 提交 TargetTick=2、Sequence=1 的 Move request。
4. 驗證 Queued=true，但位置尚未改變。
5. 第一次 Step：tick 1 沒有執行結果，位置仍為 0。
6. 第二次 Step：回傳 Accepted，玩家 X=1。
7. 從 Results 查詢同一個 action 的最終結果。

這段使用的是本專案的 GameplaySession，不是假定 framework 自帶玩家與 Move/Attack。
新專案要建立自己的 request、application handler、observation、composition；可沿用這種端口分工，不需照抄遊戲型別。

## 再閱讀完整 Demo

依順序讀：

- [GameplaySession](../../Assets/game/gameplay-simulation/src/Runtime/GameplaySession.cs)：Initialize、StepCore、handlers、Commit。
- [MovementDemoSession](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs)：frame 輸入、accumulator、唯一 realtime driver。
- [MovementDemoHost](../../Assets/game/movement-demo/src/Unity/MovementDemoHost.cs)：鍵盤、Transform、敵人 view、Overlay。
- [ReplayPlayback](../../Assets/game/gameplay-simulation/src/Runtime/ReplayPlayback.cs)：新 Manual session、重送輸入、逐 tick 比較。

不要同時把最小範例的 runner 和完整 Demo 的 session 接到同一世界，否則可能重複推進。

## 第一個功能的完成條件

- 不啟動 Unity 就能測 domain 與 tick 組裝。
- 輸入只在指定 tick 生效；正常拒絕有明確結果。
- Unity view 不反向決定 domain 位置。
- 接 Realtime 時移除其他 clock owner。
- 需要 Replay 才加入記錄與 hash；不要因為存在 framework 就一次啟用所有能力。
