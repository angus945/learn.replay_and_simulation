# TemplateRecording：錄製與播放

Demo、CLI 與測試共用 GameplayDefinition／TestableSimulationSession／TemplateReplay。正常與失敗保存同一種 TemplateRecording；舊格式停止支援的範圍見 [退休政策](../legacy-compatibility-retirement.md)。

## Unity 使用

開啟 [CharacterMovementDemo](../../Assets/game/movement-demo/scenes/CharacterMovementDemo.unity)，進入 Play Mode；左下角是 Replay 控制區。

1. WASD／方向鍵移動、Space 攻擊；從 tick 0 保留已接收的外部輸入。
2. **Save recording** 保存目前已完成的 recording boundary；即時遊玩繼續，產生新檔名而不覆寫。
3. **Load path** 載入目前格式的 JSON；正常錄製從 tick 0 暫停。舊 ReplayArtifact／FailureArtifact 不可直接載入。
4. 用 **Play／Pause／Step／Restart** 控制播放。正常重現為 Completed；重現記錄中的失敗為 ReproducedFailure；不一致為 Diverged。
5. **Return live** 返回原來的即時 session；播放期間不推進 live，呈現明確 Snap，不把 replay 狀態寫回 live。

路徑為 `Application.persistentDataPath/Replays/replay-<UTC>-<guid>.json`；離開 Play Mode 前需自行存檔。沒有自動存檔、檔案瀏覽器、倒帶或任意 seek。輸入路徑時暫停玩家鍵盤輸入，播放期間不呼叫 live input adapter。Overlay 只讀 diagnostics，可按 F3 隱藏；其效能不由 replay 一致性保證。

## API 與資料責任

- `session.CaptureRecording()` 取得不可變錄製，不停止 session；可保存正常或已 Faulted 的證據，但不能在 tick callback 中重入。
- TemplateRecording 保存已編碼 scenario／inputs、Policy、Runtime、TickDelta、實際 Limits、InitialHash、每個 TemplateTick 的 hash／ActionResults、首次 TemplateFailure 與有界 trace。
- EndTick 由 Ticks.Count 決定，保留無輸入尾段。已排隊但超過 EndTick 的輸入保存但不執行。
- 只錄製外部輸入；Internal Command／Domain Event／RNG 結果由同一 definition 重新推導，沒有錄製 Unity frame delta。
- 不保存完整 world restore checkpoint；每次從 scenario／seed 重建。失敗若未成功捕捉 observation，Diagnostics 保留上次 snapshot，附 ObservationTick。
- `TemplateRecordingIO.Write／Read` 處理 stream；呼叫端負責檔名、不覆寫與檔案容量。IO 預設讀取上限 16 MiB，可調整；Demo／CLI 使用 32 MiB。單筆 payload、總 payload、tick、input 另受 TemplateLimits 約束。
- 缺欄位、非法 schema／policy、順序、correlation、數量或 tick 邊界不被當作有效錄製。現行 tick／input 硬上限各 100,000，不是舊 reader 的百萬筆上限。

## 播放契約

`definition.CreateReplay(recording)` 建立獨立 session，對 caller 沒有 Submit／Admin，僅提供唯讀 observation／diagnostics 與播放控制。所有操作在 owner thread。

- 初始 Paused；零 tick 正常錄製直接 Completed。
- Play 後 AdvanceTime 用錄製的 TickDelta 累積時間，每 frame 最多 120 ticks，保留 backlog。
- Step 只限 Paused，恰好一 tick。Pause 清 accumulator；暫停、單步、結束的 alpha 為 1，呈現權威 tick 狀態。
- Restart 以同一 definition 重建 session、重新提交錄製輸入、清除差異；應重新取得 Diagnostics reader。Dispose 釋放 replay 擁有的 session，不影響 live。
- 每 tick 比對 ActionResult（tick、sequence、status、code）、hash 與 failure fingerprint。達 EndTick 才是 Completed 或 ReproducedFailure；第一個差異保存為 FirstDifference 並停止。
- Policy 不同在 tick 0 阻止播放；自訂 invariant 必須由同一個明確 composition 提供，不動態載入 artifact 中的程式。
- Runtime 不同回 warning；CLI 另用 GAMEPLAY_BUILD 對照 scenario.Build。相同比對結果不是跨 build／平台 bitwise determinism 保證。

## 可執行教學與驗收

先執行 [第 5 章](../../tools/gameplay-lessons/lessons/05-replay.md)：同一 CharacterMovement 加上攻擊、seeded 血量與延遲重生，驗證 JSON round trip、不同 frame 排程、Diverged 及 ReproducedFailure。

驗收仍涵蓋移動／死亡、無輸入尾段、零 tick、future input、snapshot 獨立性、不覆寫、Pause／Step／Restart、live 隔離、hash／result／policy 分歧與不完整資料拒絕。當次執行結果見 [實作進度](../implementation-progress.md)，完整接線見 [Demo 整合](../framework-guide/demo-template.md)。

## 歷史驗證（舊格式，基準 22f6966 前）

以下是原 ReplayArtifact 實驗紀錄，不是目前 TemplateRecording／API 退休後的驗收數字。

2026-08-30：Unity EditMode 107/107 通過（Replay 新增 12 cases），純 .NET checks 通過。
Unity 現場存檔／載入／播放完成 tick 83、玩家 X=2.00000072，無分歧；Restart 後單步至 tick 1，
Return live 返回原本 tick 83。控制面板已做 Game View 畫面檢查。
