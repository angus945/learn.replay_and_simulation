# Gameplay 生命週期、phase 與 seeded RNG

現行組裝為 GameplayDefinition → TestableSimulationSession → GameplayWorld／GameplayActions，沿用 registry 與 seeded-random module，不引入 ECS。本文主體描述目前玩法；末段保留基準 `22f6966` 前的歷史實驗數字，當次驗收見 [實作進度](../implementation-progress.md)。

## 1. Runtime 生命週期

正式玩法：Attack input → 模板 InputCommand → GameplayActions → GameplayWorld 發布 ActorDied → SpawnEnemy internal command → StructuralCommit。
ActorDied 只在 RequestDestroy 首次成功時安排重生；玩家死亡不重生敵人。
StructuralCommit 先 commit destroy／移除 movement repository，再生成並 commit 新敵人。
這是同一個 phase 中的兩個 registry commit，不宣稱原子交易；失敗時沒有 rollback。

新敵人固定生成在 (1,0)，新 ID 單調遞增，registry slot 可重用但 generation 更新。
新敵人在本 tick commit 後可被 observation 看見，下一 tick 才能接受 Move/Attack 或參與 movement。
本 tick 早先提交的猜測新 ID 會得到 actor.unknown／target.unknown。
同 tick 重複攻擊死者得到 target.dead，不重複生成；舊 ID 永遠不指向新角色。

GameplayWorld 在 StructuralCommit 結束前檢查 registry active、domain alive 與 movement repository membership 一致，並檢查沒有無主 registry／repository object。
`session.Gameplay.Observe().Lifecycle` 是 project diagnostic snapshot，提供 Active／RepositoryCount／RetainedActors／PendingSpawns／EnemiesSpawned；不回傳可變 registry。
CreateTestSession／Admin.Reset 的 scenario 建立測試條件，Gameplay API 沒有任意 Spawn／SetHealth。

`RespawnEnemies` 預設 false。`MaxEnemySpawns` 預設 128（包含初始敵人），啟用新功能時允許 1..4096。
達預算後不再重生，記 spawn.budget trace，不把正常預算結束當成 fault。
Tombstone 保留到 Reset 以維持舊 ID 診斷，數量最多 MaxEnemySpawns，加上玩家一筆；不是無上限保存。
這不是無限敵人遊戲的最終清理策略，也不是動態任意數量 actor 的完整管理系統。

## 2. Tick／失敗語意

Pipeline 提供可選 onPhase(phase, entering) 診斷 callback。
每個 authoritative phase 有 begin/end，包括空 phase；只有正常完成（含 reaction drain）才會發出 end。
發生 exception 時不偽造 end，後續 phase 不執行；callback 自身應唯讀且不丟例外。
PresentationRender 不屬於 authoritative tick，不加入 tick phase trace。

| Phase | 專案責任與邊界 |
|---|---|
| IntentAcquisition | 收集外部 intent；不直接寫 domain |
| IntentHandling | intent → command；command 修改 domain、產生 event，reaction drain 至穩定 |
| PrePhysics | 活著角色的 movement；新生成者此時尚未存在 |
| Physics | 此 GameplayDefinition 未註冊物理 participant；Unity framework 的選配 sensor adapter 不參與此 Demo 玩法 |
| PostPhysics | 預留物理結果整合與其 reactions |
| StructuralCommit | 先移除，再生成；同步 registry/repository。生成不能回頭參與已結束 phase |
| PresentationCapture | 取得呈現狀態，不應再修改 authoritative lifecycle |
| StateHash／Invariant | TestableSimulationSession 在 pipeline 後 capture observation、建立 hash、評估 invariant；生命週期一致性已在 commit 檢查 |

這些是 participant contract；framework 不會攔截任意 C# domain setter。
晚於 structural commit 的生命週期寫入不受本切片支援，不應靠 drain 順序偷偷繞過邊界。

TestableSimulationSession 捕捉首次 TemplateFailure：Stage、Tick（嘗試執行）、LastCompletedTick、
Sequence、Code、ExceptionType、Detail。若本 tick 尚未成功 capture，diagnostics 仍回上次成功 snapshot，ObservationTick 標示其來源；不承諾取得當下部分世界。失敗後禁止繼續 Step，必須 Reset。
已接受且改變 domain 的 action 不回滾；同 tick 未完成 action 繼續依既有規則記 tick.aborted。
進入新 phase 清 action correlation，位移／phase 級錯誤不誤指最後一個 action。
Invariant 失敗時 hash 可能已保存，但 LastCompletedTick 不前進。

尚未執行的未來 action 在 Stop/Fault 取消；Find 回 CancellationReason：session.stopped／session.faulted／tick.budget。
它們沒有假造的 ActionResult。Reset 換 identity，舊 session 的查詢是 StaleSession。
TemplateReplay 比較 failure fingerprint，包含 tick／stage／last-completed／sequence／code／exception type；失敗與正常錄製都使用 TemplateRecording。舊 FailureRerun／artifact API 已退役，參考 [退休政策](../legacy-compatibility-retirement.md)。
低階 runner 也會保存第一次 Failure 並拒絕續跑；SimulationSession 管理 Faulted／Reset，TestableSimulationSession 保存可重播證據。三者都不承諾 rollback。

## 3. 隨機敵人血量與決定性

`EnemyHealthMin/Max` 是含上下界的整數範圍；兩者為 0 表示使用固定 Health。
啟用時必須 Min>=1、Max>=Min、Max<int.MaxValue。
敵人初始生成與每次重生時抽一次 bounded integer；其他 action、失敗攻擊、Observation／Render 不抽。
使用 SplitMix64 v1、固定 health stream ID=1，由 scenario.Seed 重建；Reset 回相同狀態。
「抽一次 bounded integer」可能因無偏抽樣 rejection 而消耗多個原始 draw，不宣稱固定消耗一個 UInt64。
玩家血量仍使用 Health，傷害固定；隨機性僅來自敵人血量與下述重生延遲。

GameplayDefinition 的現行 canonical state 固定包含 tick、兩條 RNG state、累計生成數、待重生 ticks 與依 ID 排序的 actor state；scenario／seed 保存在 recording，PolicyId 識別規則、codec、hash 與 invariant 組裝。舊 hash layout 1／2／3 已不在現行程式投影。
Replay 從 seed／scenario／輸入重新生成世界，不播放內部 command/event，不做 snapshot restore。

## Demo 手動驗證

Unity Demo 啟用重生、血量 20..40、隨機重生延遲 1..3 秒、最多生成 128 隻。靠近敵人按 Space：
擊殺後觀察敵人新 ID／MaxHealth，繼續攻擊會選擇新 active enemy。
呈現使用目前 active enemy，不再固定取 observation 第 2 筆（死亡 tombstone）。
Save recording → Load path → Play，確認死亡、重生、血量與 hash 均一致。
Overlay 只讀 diagnostics，可按 F3 隱藏；不要以顯示或 frame 效能取代逐 tick 證據比對。

### 隨機重生延遲

`RandomRespawnDelay=true` 時，死亡當 tick 的 StructuralCommit 移除敵人並排程，
在 `[ceil(1/TickDelta), floor(3/TickDelta)]` 中均勻抽取整數 tick 延遲。
到期 tick 的 StructuralCommit 才生成，故等待時間介於 1..3 simulation 秒，精度為一個 tick；不是 wall-clock timer。
初始敵人仍立即生成。等待期間沒有 active enemy，畫面隱藏敵人；暫停 tick 也暫停倒數。
啟用需 RespawnEnemies=true，TickDelta 必須能表示這段範圍（<=3 秒且 tick 數小於 int.MaxValue）。

延遲使用獨立 stream ID=2；每個實際可排程的死亡抽一次，不影響 health stream ID=1。
達生成上限不排程、不抽取延遲；等待中的生成也占用預算。重複攻擊死者不會重新抽取或增加排程。
Observation 提供不可變 PendingRespawnTicks 與 RespawnRandomState；現行 canonical state 包含它們，預設 policy 為 gameplay-template-v1/splitmix64/lifecycle-v3。
Reset 清空排程並重建 seed；Stop/Fault 不再推進計時，保留排程作為凍結狀態證據。
RandomRespawnDelay=false 時仍是 commit 立即重生；這是現行 scenario 的選項，不代表目前 reader 支援舊格式。歷史錄製原樣保留，需要舊工具時使用基準 `22f6966`。

直接執行 [第 5 章](../../tools/gameplay-lessons/lessons/05-replay.md)可驗證 seeded 血量與延遲重生的 modern JSON replay。更完整的邊界案例見 [生命週期測試](../../Assets/game/gameplay-simulation/tests/LifecycleAndRandomTests.cs)。

## 歷史驗證（基準 22f6966 前）

下列數字是舊 facade／artifact 時期的實驗記錄，保留作對照，不是本次退休後的測試結果。

延遲功能驗證：Unity EditMode 145/145 通過；新增到期邊界、等待期間不重抽、血量 stream 獨立、
Reset、不同 seed、生成預算與 30/144 FPS／不規則 frame delta 的 JSON replay 測試；純 .NET 檢查通過。

### 初次生命週期補強驗證（2026-08-30）

- Unity EditMode：141 passed、0 failed、0 skipped。
- 純 .NET gameplay-checks：全部通過，包含舊 failure artifact 重跑相容性。
- 自動化矩陣涵蓋連續重生、過期 ID、生成預算、Reset RNG、不同 seed、30/60/144 FPS 與不規則 frame delta 的 replay。
- Editor Play Mode 實際 Demo：連續攻擊觀察敵人 ID/MaxHealth 為 2/37、3/24、4/23、5/38、6/40；錄製後重播至 tick 711，Completed、FirstDifference=null。
- QA replay 留在 persistentDataPath/Replays，未修改 scene；驗證後返回 Edit Mode。
