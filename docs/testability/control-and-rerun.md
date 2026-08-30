# 工具控制契約與重跑診斷

現行工具、Demo、測試及 Protocol adapter 共用 GameplayDefinition／TestableSimulationSession。正常與失敗證據都使用 TemplateRecording；舊 GameplaySession／artifact reader 已退役，歷史基準與原始檔案政策見 [退休政策](../legacy-compatibility-retirement.md)。

## API：只分發 consumer 需要的能力

以 `new GameplayDefinition().CreateTestSession(scenario)` 建立已 Running 的 session，不另呼叫 Start。對 gameplay、測試工具、Overlay 分發各自的 ports；不要直接交出完整 session。

| 屬性／接點 | API | 用途 |
| --- | --- | --- |
| Gameplay | ITemplateGameplay<GameplayInput, GameplayObservation> | Submit(sessionId, sequence, targetTick, input)、Observe、ID 與 tick |
| Simulation | ITemplateSimulation | 手動 Step，取得 TemplateTick |
| Admin | ITemplateAdmin<GameplayScenario> | Reset、Stop |
| Results | ITemplateResults | Find、完成結果分頁 Read |
| Diagnostics | IDiagnosticReader<GameplayObservation> | 唯讀 observation、invariant、trace |
| Session 唯讀資訊 | Policy、Limits、InvariantReport | 實際執行政策、預算與最近評估 |
| Composition | CreateRealtimeRunner、CaptureRecording | 建立即時驅動者、保存錄製 |

各 port 不實作其他控制介面；這是工程邊界，不是防止惡意同程序程式的安全沙箱。Gameplay port 沒有 SetHealth／Spawn／Reset。玩法描述與 Action Catalog 屬於 [game Protocol adapter](../../Assets/game/gameplay-protocol/README.md) 的 capabilities.read，不是 generic framework 的 domain discovery。

## 呼叫規則與驅動權

- Session／callbacks 在同一 owner thread、ticks 之間呼叫；不是 thread-safe API。
- 預設手動 Step。CreateRealtimeRunner 取得唯一 tick 驅動權，framework 管理 frame accumulator 與補 tick 上限；掛上 runner 時不能手動 Step／Reset／Dispose session。
- Runner 提供 Pause／Resume；要回手動控制，先 Dispose runner。Reset 是重建 world／identity，不是倒轉已執行的狀態。
- Submit 只回 admission；同 tick 按 sequence 排序，最終 ActionResult 由 tick 或 Results 查詢，不從 trace 推測。
- Find 使用 session ID＋sequence，結果為 Unknown／Pending／Completed／Cancelled／StaleSession。Stop／Fault 取消尚未執行的輸入，沒有捏造 ActionResult；已完成結果即使該 tick 後來失敗也保留。
- Results.Read 的 afterIndex 是已讀完成結果數，不是 action sequence；單頁 1–1024 筆，NextIndex 連同 session ID 保存。結果保留到 Reset、受輸入預算限制，不受 trace overwrite 影響。
- Snapshot／結果頁是不可變副本；查詢不 Step、不重新評估 invariant。DiagnosticSnapshot.ObservationTick 表示快取 observation 的來源 tick，不一定等於失敗 tick。
- 省略 TemplateLimits 時，初建與 Reset 都依新 scenario 導出預設預算；明確傳入 limits 則保留覆寫。候選世界／checks／hash 初始化失敗時保留原 session 與 limits。

用法可直接執行 [第 4 章](../../tools/gameplay-lessons/lessons/04-testability.md)；完整時序與故障邊界見 [Testability／Replay 模板](../framework-guide/testability-replay-template.md)。

## 正常與失敗的重現

CaptureRecording 保存 scenario、Policy、Runtime、實際 limits、InitialHash、外部 inputs、逐 tick results／hash、首次 TemplateFailure 與有界 trace。失敗錄製不需要第二種 artifact。

TemplateReplay 以相同 definition 建立乾淨世界。正常結束為 Completed；在預期 tick 重現相同 failure fingerprint 為 ReproducedFailure；第一個 policy／hash／result／failure 差異為 Diverged，附 FirstDifference 的 category、tick、expected、actual。ReproducedFailure 表示重現成功，不表示玩法成功。

PolicyId 由專案識別 gameplay／codec／hash／invariant 版本，不是程式碼雜湊。自訂 invariant 用 factory 每個 session 建立新實例，並提供明確 policy；不從 artifact 動態載入程式。Runtime／build 不同只警告，不承諾跨環境決定性。比較不依賴不穩定 stacktrace、session GUID 或 trace overwrite。

## CLI

在專案根目錄執行；capture 的路徑必須是尚不存在的新檔：

```text
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- capture <new-failure-recording.json>
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- capture-success <new-success-recording.json>
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- rerun <recording.json>
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- rerun docs/testability/failure-template-example.json
```

- 無參數執行接入的 headless contract checks，包含 core／testability／game 與現行 Protocol adapter；不載入 Unity assembly。
- capture：正式 Move input 在 tick 2 觸發 cli.position_limit oracle（x > 0.5），示範沒有 exception 的失敗；recording 保存明確自訂 policy，rerun 建立同一 oracle。
- capture-success：對角 Move → Stop，共八 ticks，保存正常 TemplateRecording。
- rerun：輸出 Completed／ReproducedFailure／Diverged 與 FirstDifference。未知 policy 不動態載入程式，也不當作相容。
- 寫入採 CreateNew，不覆寫舊檔。CLI 讀取先檢查 32 MiB 上限；TemplateRecordingIO 也在反序列化前限制實際 bytes。
- Exit 0：capture 成功或 replay 證據符合；2：重播差異；1：命令、檔案、schema／codec 或組裝錯誤。
- Tick／input 上限由 recording 的 TemplateLimits 約束，最大各 100,000。沒有 callback watchdog、不可信程式 sandbox 或自動探索器。
- GAMEPLAY_BUILD 識別目前 build；缺少回 build.unverified，不符回 build.mismatch，runtime 不同也只警告。不得拿 artifact 自報的 build 當成目前執行版本。

現行失敗樣本為 [failure-template-example.json](failure-template-example.json)。[failure-example.json](failure-example.json) 保留原始歷史 bytes，不能交給目前的 rerun；舊讀取路由已移除。需要研究舊格式時使用基準 22f6966 的原工具，見 [退休政策](../legacy-compatibility-retirement.md)，不在目前版本恢復相容 facade。

Protocol adapter 使用 game payload v2，envelope 仍為 v1；權限／lease／去重仍在 in-process boundary。Transport、外部 client、連線恢復與 Explorer 維持 Deferred。

## 驗證入口與歷史範圍

CLI 應驗證正常及 invariant-failure round trip、hash／policy 篡改、檔案不覆寫、錯誤格式與容量拒絕；當次結果以 [實作進度](../implementation-progress.md)為準，不沿用下列舊格式測試總數。

以下保留的是基準 `22f6966` 之前的歷史實驗，不是現行 API 或本輪測試證據。

## 歷史驗證紀錄（舊格式）

2026-08-30：Unity Editor 編譯無錯誤，EditMode **95/95 通過**（本輪新增 10 項）。
純 .NET checks 通過；CLI capture → rerun 新 artifact 比較符合，舊 schema 1 範例也比較符合，
舊檔如預期回 policy.unverified。未改場景、未變更 Overlay，未加入網路 Protocol。
