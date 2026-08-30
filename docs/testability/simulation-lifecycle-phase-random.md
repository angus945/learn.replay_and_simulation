# Simulation／Testability 三階段補強

本輪不修改 Protocol 或診斷 Overlay 的產品程式；Overlay 測試改為依實際 trace gap 斷言，不寫死新增 phase trace 前的數量。仍使用既有 registry、seeded-random module，不引入 ECS／Physics。

## 1. Runtime 生命週期

正式玩法：Attack → ExecuteAction → ActorDied → SpawnEnemy internal command → StructuralCommit。
ActorDied 只在 RequestDestroy 首次成功時安排重生；玩家死亡不重生敵人。
StructuralCommit 先 commit destroy／移除 movement repository，再生成並 commit 新敵人。
這是同一個 phase 中的兩個 registry commit，不宣稱原子交易；失敗時沒有 rollback。

新敵人固定生成在 (1,0)，新 ID 單調遞增，registry slot 可重用但 generation 更新。
新敵人在本 tick commit 後可被 observation 看見，下一 tick 才能接受 Move/Attack 或參與 movement。
本 tick 早先提交的猜測新 ID 會得到 actor.unknown／target.unknown。
同 tick 重複攻擊死者得到 target.dead，不重複生成；舊 ID 永遠不指向新角色。

每 tick 完成後檢查 registry active、domain alive 與 movement repository membership 一致，並檢查沒有無主 registry／repository object。
`ObserveLifecycle()` 是 project diagnostic snapshot，提供 active/repository/retained/pending/generated 計數；不回傳可變 registry。
Admin 的 Start/Reset scenario 建立測試條件，Gameplay API 沒有任意 Spawn／SetHealth。

`RespawnEnemies` 預設 false。`MaxEnemySpawns` 預設 128（包含初始敵人），啟用新功能時允許 1..4096。
達預算後不再重生，記 spawn.budget trace，不把正常預算結束當成 fault。
Tombstone 保留到 Reset 以維持舊 ID 診斷，數量最多 MaxEnemySpawns，加上玩家一筆；不是無上限保存。
這不是無限敵人遊戲的最終清理策略，也不是動態任意數量 actor 的完整管理系統。

## 2. Tick／失敗語意

Pipeline 新增可選 onPhase(phase, entering) 診斷 callback。
每個 authoritative phase 有 begin/end，包括空 phase；只有正常完成（含 reaction drain）才會發出 end。
發生 exception 時不偽造 end，後續 phase 不執行；callback 自身應唯讀且不丟例外。
PresentationRender 不屬於 authoritative tick，此次沒有加入其 trace。

| Phase | 專案責任與邊界 |
|---|---|
| IntentAcquisition | 收集外部 intent；不直接寫 domain |
| IntentHandling | intent → command；command 修改 domain、產生 event，reaction drain 至穩定 |
| PrePhysics | 活著角色的 movement；新生成者此時尚未存在 |
| Physics | 預留引擎適配，本專案尚無 participant |
| PostPhysics | 預留物理結果整合與其 reactions |
| StructuralCommit | 先移除，再生成；同步 registry/repository。生成不能回頭參與已結束 phase |
| PresentationCapture | 取得呈現狀態，不應再修改 authoritative lifecycle |
| StateHash／Invariant | GameplaySession 在 pipeline 後建立 hash、驗證生命週期／invariant |

這些是 participant contract；framework 不會攔截任意 C# domain setter。
晚於 structural commit 的生命週期寫入不受本切片支援，不應靠 drain 順序偷偷繞過邊界。

GameplaySession 捕捉第一次失敗：FailureStage、FailureTick（嘗試執行）、LastCompletedTick、
ActionSequence、exception type、當下部分狀態與已收集證據。失敗後禁止繼續 Step，必須 Reset。
已接受且改變 domain 的 action 不回滾；同 tick 未完成 action 繼續依既有規則記 tick.aborted。
進入新 phase 清 action correlation，位移／phase 級錯誤不誤指最後一個 action。
Invariant 失敗時 hash 可能已保存，但 LastCompletedTick 不前進。

尚未執行的未來 action 在 Stop/Fault 取消；Find 回 CancellationReason：session.stopped／session.faulted／tick.budget。
它們沒有假造的 ActionResult。Reset 換 identity，舊 session 的查詢是 StaleSession。
FailureRerun 會在新 artifact 有 FailureStage 時比較 stage／last completed tick；舊檔缺欄位不強制比較。
Framework runner 本身傳出例外、不承諾 rollback；fault 鎖定與證據保存由 GameplaySession 負責。

## 3. 隨機敵人血量與決定性

`EnemyHealthMin/Max` 是含上下界的整數範圍；兩者為 0 表示舊行為（使用 Health）。
啟用時必須 Min>=1、Max>=Min、Max<int.MaxValue。
敵人初始生成與每次重生時抽一次 bounded integer；其他 action、失敗攻擊、Observation／Render 不抽。
使用 SplitMix64 v1、固定 health stream ID=1，由 scenario.Seed 重建；Reset 回相同狀態。
「抽一次 bounded integer」可能因無偏抽樣 rejection 而消耗多個原始 draw，不宣稱固定消耗一個 UInt64。
玩家血量仍使用 Health，傷害固定；没有其他隨機玩法。

啟用 respawn 或隨機血量時使用 gameplay hash schema 2，包含新增 scenario 設定、algorithm version、
RNG state、累計生成數與既有 domain state；diagnostic policy 附 lifecycle-v2。
舊 scenario 仍使用 schema 1，缺少新 DataMember 的舊 artifact 可重跑。
Replay 從 seed／scenario／輸入重新生成世界，不播放內部 command/event，不做 snapshot restore。

## Demo 手動驗證

Unity Demo 啟用重生、血量 20..40、隨機重生延遲 1..3 秒、最多生成 128 隻。靠近敵人按 Space：
擊殺後觀察敵人新 ID／MaxHealth，繼續攻擊會選擇新 active enemy。
呈現使用目前 active enemy，不再固定取 observation 第 2 筆（死亡 tombstone）。
Save recording → Load path → Play，確認死亡、重生、血量與 hash 均一致。
既有 Overlay 效能问题仍未修復，必要時 F3 隱藏。

### 隨機重生延遲

`RandomRespawnDelay=true` 時，死亡當 tick 的 StructuralCommit 移除敵人並排程，
在 `[ceil(1/TickDelta), floor(3/TickDelta)]` 中均勻抽取整數 tick 延遲。
到期 tick 的 StructuralCommit 才生成，故等待時間介於 1..3 simulation 秒，精度為一個 tick；不是 wall-clock timer。
初始敵人仍立即生成。等待期間沒有 active enemy，畫面隱藏敵人；暫停 tick 也暫停倒數。
啟用需 RespawnEnemies=true，TickDelta 必須能表示這段範圍（<=3 秒且 tick 數小於 int.MaxValue）。

延遲使用獨立 stream ID=2；每個實際可排程的死亡抽一次，不影響 health stream ID=1。
達生成上限不排程、不抽取延遲；等待中的生成也占用預算。重複攻擊死者不會重新抽取或增加排程。
Observation 提供不可變 PendingRespawnTicks 與 RespawnRandomState；新 hash schema 3 包含它們，policy 為 lifecycle-v3。
Reset 清空排程並重建 seed；Stop/Fault 不再推進計時，保留排程作為凍結狀態證據。
未設定新選項的舊 scenario／replay 維持原先立即重生與 hash schema 1/2，不改寫舊錄製。

延遲功能驗證：Unity EditMode 145/145 通過；新增到期邊界、等待期間不重抽、血量 stream 獨立、
Reset、不同 seed、生成預算與 30/144 FPS／不規則 frame delta 的 JSON replay 測試；純 .NET 檢查通過。

## 本輪驗證（2026-08-30）

- Unity EditMode：141 passed、0 failed、0 skipped。
- 純 .NET gameplay-checks：全部通過，包含舊 failure artifact 重跑相容性。
- 自動化矩陣涵蓋連續重生、過期 ID、生成預算、Reset RNG、不同 seed、30/60/144 FPS 與不規則 frame delta 的 replay。
- Editor Play Mode 實際 Demo：連續攻擊觀察敵人 ID/MaxHealth 為 2/37、3/24、4/23、5/38、6/40；錄製後重播至 tick 711，Completed、FirstDifference=null。
- QA replay 留在 persistentDataPath/Replays，未修改 scene；驗證後返回 Edit Mode。
