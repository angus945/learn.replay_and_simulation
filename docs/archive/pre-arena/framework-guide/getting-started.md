# 入門：從最小玩法到正式控制面

[回到索引](README.md)

## 先執行範例

在 repository 根目錄，使用 .NET 9 SDK 或相容 SDK：

```powershell
dotnet run --project tools/gameplay-lessons -- all
```

不需開啟 Unity。[五章路線](learning-path.md)由同一 CharacterMovement 累加 Domain、Application、Definition、Testability 與 Replay；任一斷言失敗回傳非零 exit code。第一次閱讀以此為主，不必先做另一套 Player／CubeActor 範例。

以下 A 補充低階機制，B 對應現行正式入口；都連到實際參與編譯的來源，不另維護脫離程式的完整副本。

## 範例 A：最小 Domain → Pipeline

如果需要理解 Definition 底下做了什麼，閱讀 [FrameworkGuideExamples.MinimalMovement](../../tools/gameplay-checks/FrameworkGuideExamples.cs)，依序理解：

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

此補充來源在 `dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj` 編譯並執行。[第 3 章](../../tools/gameplay-lessons/lessons/03-simulation.md)則使用 MovementDefinitionExample 代為管理 Create／Reset／Dispose，兩種入口共用既有 domain，不能一起驅動同一世界。

### 新專案需要什麼依賴？

重用框架時，最小核心是 `Module.SimulationPrimitives`、`Module.WaveDispatcher`、`Framework.DeterministicSimulation`。
本範例另使用本 repository 的 CharacterMovement Domain／Application／Integration assemblies，方便展示組裝；新遊戲應以自己的領域替換。
目前 module 位於 Assets，不是可直接填入 UPM manifest 的套件名稱。
搬移到新 Unity 專案時需保留來源及 .meta，並透過 asmdef 指定依賴；勿複製 Library 或 Unity 生成的 csproj。

## 範例 B：正式操作、排序與結果

執行 `dotnet run --project tools/gameplay-lessons -- testability`，閱讀 [Stage04Testability](../../tools/gameplay-lessons/Stage04Testability.cs)：

1. 建立 GameplayDefinition，以無敵人、tickDelta=.25、4 ticks／4 inputs 預算的 scenario 呼叫 CreateTestSession；回傳已 Running 的 session，不再呼叫 Start。
2. 從 observation.PlayerId 取得玩家，不用 Actors[0] 猜身分。
3. 向 tick 2 先提交 sequence 2 向左，再提交 sequence 1 向右，接著提交 unknown actor 的 sequence 3。
4. 三筆 envelope 都 Queued，但位置未變；重複 sequence 在 admission 拒絕。
5. tick 1 沒有結果；tick 2 按 sequence 1、2、3 執行。兩次方向變更 Accepted，未知角色回 actor.unknown。
6. 最後方向向左，再於 PrePhysics 移動，玩家 X=-1；舊 snapshot 仍為 X=0。
7. Results.Find 取得執行結果；Reset 重建世界與 identity，舊 identity 提交回 session.stale。

GameplayDefinition／GameplayWorld／GameplayActions 是本專案的組裝與玩法，framework 不自帶玩家與 Move／Attack。新專案提供自己的 input、application／domain、observation 與 definition；沿用端口分工，不需照抄所有遊戲型別。

FrameworkGuideExamples 的 `ControlledMovement()` 也使用 GameplayDefinition／模板 ports：一筆 tick 2 輸入，驗證 Queued 與 Accepted 分開、玩家 X=1 及 Results 查詢。它是較短的補充範例，沒有另一套 facade 或玩法。

## 再閱讀完整 Demo

依順序讀：

- [GameplayDefinition](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)：world factory、codec、canonical state、invariant／trace metadata。
- [GameplayActions](../../Assets/game/gameplay-simulation/src/Runtime/GameplayActions.cs)與[GameplayWorld](../../Assets/game/gameplay-simulation/src/Runtime/GameplayWorld.cs)：玩法決策、domain 組合與生命週期接線。
- [MovementDemoSession](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs)：frame 輸入與呈現 callbacks；時間累積交給唯一 realtime runner。
- [MovementDemoHost](../../Assets/game/movement-demo/src/Unity/MovementDemoHost.cs)與[GameplayActorPresentation](../../Assets/game/movement-demo/src/Unity/GameplayActorPresentation.cs)：鍵盤、按 ID 綁定的 pooled view、Overlay。
- [TemplateReplay](../../Assets/framework.testability/src/API/TemplateReplay.cs)：依同一 definition 重建獨立 session、重送輸入、逐 tick 比較；可先執行[第 5 章](../../tools/gameplay-lessons/lessons/05-replay.md)。

不要同時把最小範例的 runner 和完整 Demo 的 session 接到同一世界，否則可能重複推進。
上述步驟與現行錄製不需要先接協定。Protocol adapter 接現行 ports，transport 仍暫緩；舊 GameplaySession／ReplayPlayback 已退役，歷史工具見 [退休政策](../legacy-compatibility-retirement.md)。

## 第一個功能的完成條件

- 不啟動 Unity 就能測 domain 與 tick 組裝。
- 輸入只在指定 tick 生效；正常拒絕有明確結果。
- Unity view 不反向決定 domain 位置。
- 接 Realtime 時移除其他 clock owner。
- 需要 Replay 才加入記錄與 hash；不要因為存在 framework 就一次啟用所有能力。
