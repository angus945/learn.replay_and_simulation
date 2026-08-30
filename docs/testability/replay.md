# 最小 Replay 錄製／播放

專案整合層功能，未抽成 module，未增加 Protocol。既有 diagnostics Overlay 保持原樣。

## Unity 使用

開啟 CharacterMovementDemo，進入 Play Mode；左下角是 Replay 控制區。

1. 正常 WASD／方向鍵移動、Space 攻擊；session 從 tick 0 自動保留已接收輸入。
2. **Save recording** 保存到按下時的成功 tick。即時遊玩繼續，新檔名不覆寫舊檔。
3. 路徑自動填入文字框，按 **Load path** 載入；也可貼上以前的 replay JSON 完整路徑。
4. 載入後在 tick 0 暫停；用 **Play / Pause / Step / Restart** 控制播放。
5. **Return live** 返回原本的即時 session；播放期間它沒有推進，錄製不包含播放所花的時間。

路徑為 `Application.persistentDataPath/Replays/replay-<UTC>-<guid>.json`，完整路徑也印到 Console。
必須在離開 Play Mode 前存檔；沒有自動存檔、檔案瀏覽器、倒帶或任意 seek。
輸入路徑時暫停玩家鍵盤輸入。播放期間完全不呼叫即時輸入 adapter。
舊 failure artifact 不是 replay artifact，不可混用；failure 繼續走原本 rerun CLI。
Overlay 仍有已知文字繪製長幀問題；可按 F3 隱藏，本輪沒有修正它。

## API 與資料責任

- `GameplaySession.CaptureReplay()`：成功 tick 邊界的不可變快照，不改變 session、不停止錄製。
- `ReplayArtifact` schema 1：scenario（包含 seed/build）、runtime、diagnostic policy、EndTick、
  所有已接收 GameplayRequest、已執行 ActionResult、tick 0 到 EndTick 的完整 hash checkpoints。
- EndTick 獨立於最後輸入 tick，保留無輸入尾段。已排隊但超過 EndTick 的輸入保存但不執行。
- 只保存外部輸入；Intent adapter 重新建立 Internal Command／Domain Event。沒有錄製 Unity frame delta。
- 不保存快照還原資料；每次從 scenario 初始化世界。結束 domain state 由最後 hash 驗證，
  不把 recorder 的 Running/Stopped 管理狀態當作 gameplay 狀態。
- Created／Faulted／tick 中不能 CaptureReplay；fault 用 FailureArtifact。Stop 後仍可保存成功錄製。
- `ReplayFile.SaveNew/Load`：JSON、32 MiB 上限、CreateNew、不覆寫；缺失資料、schema、順序、
  correlation／數量／tick 邊界錯誤拒絕載入。播放上限 1,000,000 ticks/actions，另受 scenario 預算約束。

## 播放契約

`ReplayPlayback : IReplayPlayback` 獨占新 Manual session，不對 caller 暴露 Submit／Admin。
僅提供唯讀 observation／diagnostics。Single-threaded，呼叫者在 owner thread 驅動。

- 初始 Paused，零長度錄製直接 Completed。
- Play 後 AdvanceTime 用錄製的 TickDelta 累積時間；每 frame 最多 120 ticks，保留未消耗時間。
- Step 僅限 Paused；推進恰好一 tick，畫面顯示該 tick 狀態。
- Pause 清 accumulator 並顯示目前權威狀態；不是保留任意子 tick 畫面時間的精密播放器。
- Restart 新建 session，回 tick 0、清除差異與計數；自訂 factory 必須每次回傳 fresh Manual session。
- 達 EndTick 自動 Completed，不額外推進。正常播放有插值，暫停／單步／結束 snap 至權威狀態。
- 每 tick 比較 ActionResult（tick、sequence、status、code）、state hash；異常或 invariant fault 也停止。
- 第一個差異設為 FirstDifference（category／tick／expected／actual）並進入 Diverged；不繼續播放。
- policy 不一致在 tick 0 阻止播放；自訂 invariant 必須由相同 composition/factory 提供。
- build 未指定／不符、runtime 不符回 Warnings；不宣稱跨 build／平台 bitwise 一致。
  可用 GAMEPLAY_BUILD 標示目前程式版本，不能把 artifact 內的版本當成目前執行版本。

## 驗證範圍

測試涵蓋 30/60/144 FPS、不規則 frame delta、移動、攻擊／死亡、無輸入尾段、
正常錄製 JSON round trip、不覆寫、零長度、future input、snapshot 獨立性、暫停／單步／重播、
Realtime Demo 到 Manual Replay、hash/result/policy 分歧與不完整資料拒絕。
Headless checks 也包含正常 replay round trip，沒有 Unity assembly 依賴。

2026-08-30：Unity EditMode 107/107 通過（Replay 新增 12 cases），純 .NET checks 通過。
Unity 現場存檔／載入／播放完成 tick 83、玩家 X=2.00000072，無分歧；Restart 後單步至 tick 1，
Return live 返回原本 tick 83。控制面板已做 Game View 畫面檢查。
