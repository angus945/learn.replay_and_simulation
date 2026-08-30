# 最小唯讀 Diagnostics Overlay

在既有 CharacterMovementDemo Play 時自動建立（僅 Debug.isDebugBuild）。F3 顯示／隱藏；不提供 Reset、Step、Submit 或狀態修改。
Overlay 可見時隱藏舊 HUD；隱藏後恢復舊 HUD。WASD／方向鍵與空白鍵仍由原 gameplay adapter 處理。

## 邊界

- `src/Model`：純 C# 增量讀取 consumer，僅持有 IDiagnosticReader<TObservation>。
- `src/Unity`：專案的 GameplayObservation 面板，顯示 session、tick、角色、invariants 和 trace。
- `tests`：無 gameplay mutation、無額外 invariant evaluation、stream reset、資料缺口與 local history 容量。

GameplaySession.Diagnostics 回傳獨立 facade，不能直接 cast 成 Session 或 Gameplay control。
這是工程上的唯讀能力隔離，不是防止同程序反射的安全沙箱。
面板不讀 Transform／Domain entity，不重算 invariant，不透過 static singleton 找世界。

## 更新與顯示

可見時每 0.2 秒 poll，讀一頁最多 256 筆，local history 最多 200 筆。
顯示 newest first，scroll 只操作本地 UI。
Missed 是未讀就被來源覆蓋的紀錄；Source overwritten 是來源累計覆蓋；Local trimmed 是面板自己的顯示歷史淘汰，三者不混用。
面板隱藏時不 poll，重新開啟會回報期間的缺口；這不影響 simulation。
Session reset 自動清除 local history，重新顯示新 session。
Invariant 顯示快取的最近完成評估：NOT EVALUATED／PASS／FAIL；不是目前 tick 的結果時加 STALE。
異常中斷的 tick 同時顯示 Fault code，不把上一 tick 的 PASS 當作本 tick 成功。

本輪不是通用 schema-driven Overlay，也未建立 module.observation、Protocol 或遠端權限層。
