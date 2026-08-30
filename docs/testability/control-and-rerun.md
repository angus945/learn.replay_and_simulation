# 工具控制契約與重跑診斷

本輪停在 in-process API；沒有網路服務、Protocol DTO、通用 Overlay。
Overlay 與 GameplayObservation 維持本專案用途。程式沿用 src/API、src/Contract、src/Runtime、tests 結構。

## API：可呼叫哪些操作

GameplaySession 是 composition owner；對 consumer 分發獨立 facade，不直接傳 session。

| 屬性 | API | 用途 |
|---|---|---|
| Gameplay | IGameplayControl | Submit、Observe、session ID 與 tick |
| Simulation | ISimulationControl | 手動 Step、讀取 DriveMode |
| Admin | ITestSession<GameplayScenario> | Start、Reset、Stop |
| Results | IActionResultReader | 單筆 Find、分頁 Read |
| Capabilities | IGameplayCapabilities | Describe：模式、生命週期可用性、預算、Action Catalog |
| Diagnostics | IDiagnosticReader<GameplayObservation> | 既有唯讀 observation／invariant／trace |

各 facade 不實作其他權限介面，不能透過正常 cast 取得 owner。這不是安全沙箱。
既有 session 方法保留供 composition 相容；工具應只取得所需 facade。
Capabilities 的 CanSubmit/CanStep 表示生命週期與模式允許，不保證個別請求通過預算／參數檢查。

## Contract：呼叫規則與保證

- 全部 API 在同一 owner thread、tick 之間呼叫；不是 thread-safe API。未加入背景執行或鎖。
- 建立 session 時選 Manual（預設）或 Realtime，終身固定，Reset 不切換模式。
- Manual：Simulation.Step 每次執行一 tick，不能領取 realtime driver。
- Realtime：composition 只能領取一次 ClaimRealtimeDriver；其 AdvanceTick 才能推進。
  session.Step 與 Simulation.Step 都拒絕，不因 Admin.Reset 重新發放 authority。
  Driver 不傳给手動工具；持有者自行管理 accumulator。Demo 已接此路徑。
- 沒有 pause／mode-switch／lease-transfer；需要切模式時建立另一個 session。避免無意合併兩個 clock。
- Submit 只回 admission；最終 ActionResult 由 Results 查詢，不從 Trace 推測。
- Find 使用 session ID + sequence：Unknown / Pending / Completed / Cancelled / StaleSession。
  Stop/Fault 後尚未執行者為 Cancelled，Result 為 null；不捏造 gameplay 執行結果。
  已完成結果即使該 tick 後來發生錯誤也保留（沒有 rollback）。
- Read 的 afterIndex 是已讀「完成結果數」，不是 action sequence；從 0 開始。
  以完成順序分頁，單頁 1–1024 筆；NextIndex 與 session ID 一起保存。
  所有完成結果保留到 Reset，受 MaxActions 上限限制，與 trace overwrite 無關。
  舊 session ID、非法 cursor、tick 中讀取皆明確拒絕。
- Capability/結果 snapshot 為不可變副本；讀取不 Step、不 Submit、不重新評估 invariant。
- Action Catalog 提供 Move/Attack、actor/target/axes 要求、成功／業務拒絕代碼；
  詳細 gameplay 與 queue 規則沿用 gameplay-simulation README。

## 結構化重跑

`FailureRerun.Compare` 回 RerunReport：Executed、Matches、FirstDivergentTick、Differences、Warnings。
差異含 category、tick（若適用）、expected、actual。比較 failure code/tick/action/exception type、
所有 ActionResult 與 hash checkpoints；最早分歧 tick 取所有有 tick 差異的最小值。
沒有比較不穩定 stacktrace/session GUID，也不把 trace overwrite 當成 gameplay 分歧。

- schema 不支援／session 非 fresh Manual：不執行並給結構化錯誤。
- artifact 缺欄位、超過 scenario 邊界、無法提交等：rerun.error，Matches=false。
- diagnostic policy 是 revision + ordinal 排序 invariant codes。規則實作改變時，composition 必須更新
  `new GameplaySession(policyRevision: "v2")`；不是程式碼雜湊，也不自動發現同 code 的規則變更。
- schema 1 增加可選 DiagnosticPolicy，舊 artifact 仍能讀取，但回 policy.unverified 警告。
- policy 不符是比較失敗。缺少自訂 invariant 不會動態載入插件，需 caller 提供相同 composition。
- currentBuild 由執行方提供，不拿 artifact.Build 冒充目前版本。不同 build/runtime 回警告；
  未提供 build 回 build.unverified。Matches 僅代表比較資料相同，不保證執行環境相同。
- ScenarioRerun.VerifyFailure 保留 bool 相容入口，內部使用結構化比較。

## CLI

在專案根目錄執行：

```text
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- capture <new-artifact.json>
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj -- rerun <artifact.json>
```

無參數跑既有 headless checks；capture 另輸出 overflow 範例，CreateNew 不覆寫。
rerun 讀指定檔案並輸出 JSON report，不改來源檔。Exit 0=比較符合、2=比較不符／重跑拒絕、
1=CLI 用法／檔案／反序列化錯誤。GAMEPLAY_BUILD 環境變數可標示目前執行版本。
CLI 限制 32 MiB 輸入及 1,000,000 ticks/actions；這不是不可信程式的 sandbox，也沒有 callback watchdog。

下一階段才建立 versioned protocol DTO／mapping／main-thread ingress，維持現在的權限分離。

## 驗證紀錄

2026-08-30：Unity Editor 編譯無錯誤，EditMode **95/95 通過**（本輪新增 10 項）。
純 .NET checks 通過；CLI capture → rerun 新 artifact 比較符合，舊 schema 1 範例也比較符合，
舊檔如預期回 policy.unverified。未改場景、未變更 Overlay，未加入網路 Protocol。
