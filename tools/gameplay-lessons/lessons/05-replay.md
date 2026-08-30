# 05：錄外部輸入，重新推導結果

[上一章](04-testability.md) · [課程索引](../README.md)

問題：如何證明不是只「播得動」，而是回到相同的狀態、操作結果或失敗？

## 執行

```powershell
dotnet run --project tools/gameplay-lessons -- replay
```

## 讀來源並跟著接

1. 讀 [Stage05Replay.cs](../Stage05Replay.cs)。仍使用現有 GameplayDefinition，設定 seed 814731、敵人血量 20～40、攻擊傷害 100、延遲重生 1～3 秒、最多出生兩名敵人；攻擊／死亡／RNG／重生由現有 game 規則處理。
2. 提交三筆外部輸入：tick 1 向右、tick 1 攻擊、tick 2 停止。以 .25 秒 tick 推進 16 ticks，涵蓋最長重生延遲；確認角色停在 X=1、舊敵人 inactive、新敵人有新 ID 且 HP 在 20～40、EnemiesSpawned=2、待重生排程為空。錄製只有三筆外部 input；RNG 抽樣與 spawn 不是第四筆外部 input。
3. `CaptureRecording` 得到現行 TemplateRecording，使用 [TemplateRecordingIO](../../../Assets/framework.testability/src/Runtime/TemplateRecordingIO.cs) 在 MemoryStream 完成 JSON write／read。這是實際 codec round trip，但不寫使用者磁碟檔案。
4. 用同一 Definition 的 `CreateReplay` 建立新世界。分別以 30 FPS、144 FPS 及 .7 秒長 frame 播放，要求 Completed／無 FirstDifference。框架逐 tick 比對 hash、ActionResult 與 failure；canonical state 已包含 RNG state 與待重生 ticks，因此這裡也驗證 seeded 血量與重生排程重現。
5. 複製錄製並只改第一筆輸入為向左，保留原本預期結果；回放必須在 tick 1 報 Diverged，而不是悄悄把新結果當作成功。
6. 再由 invariant factory 注入 `lesson.position_limit`，明確指定新 PolicyId。角色連續移動到 tick 2、X=2 時違反測試界線；保存並 round trip 錄製，在乾淨世界得到 ReproducedFailure。

依賴：本章只在第 4 章組裝外增加 RecordingIO／TemplateReplay 和一個教學 oracle，沒有第二份玩法、Domain state setter 或 Protocol。

## 三種結果不能混用

| 結果 | 這章的意義 |
| --- | --- |
| Completed | 正常錄製的每個 tick 證據相符 |
| Diverged | 修改過的輸入與原本預期證據在 tick 1 不同 |
| ReproducedFailure | 相同診斷政策在 tick 2 重現 lesson.position_limit；重現成功，玩法仍是失敗案例 |

最後一行為 `PASS 05 replay`。自訂 oracle 是測試政策，沒有修改 Domain，也沒有讓玩家取得作弊 setter。

目前只驗證同 runtime 邏輯重播；不承諾跨平台 bitwise physics、任意 tick snapshot restore 或 rollback。RNG／延遲重生已納入本章同一條接線；更多規格見 [生命週期與 RNG](../../../docs/testability/simulation-lifecycle-phase-random.md)，Unity adapter 驗收另見 [實作進度](../../../docs/implementation-progress.md)。
