# Trace／Invariant 模組化與唯讀 Overlay

日期：2026-08-30。

## 決策

- module.diagnostic-trace 提供泛型 bounded journal 與分離的 reader/writer；simulation TraceEntry 仍留在 framework。
- module.invariant 擁有規則契約與註冊／評估機制；遊戲規則留在 game，評估時機與 Faulted 政策留在 Session。
- framework.testability 新增 IDiagnosticReader<T> 與 immutable DiagnosticSnapshot；只讀取快取 invariant report，不重新評估。
- GameplaySession 提供獨立 readonly facade，Reset 後 facade 可繼續讀到新 session，但 trace stream identity 改變。
- Overlay 是第一個真實 consumer，暫不抽 observation registry 或 metadata/schema 系統。
- 所有新 module 維持 src/API、src/Contract、src/Runtime 與 tests；noEngineReferences=true。

## 驗證

- Unity EditMode 初輪 85/85 passed（保留原 69 項，新增 16 項）。
- 泛型 trace 驗證分頁、缺口、stream 更換、獨立 reader、資料副本、讀写介面隔離。
- Invariant module 驗證順序、Seal、結果副本與 exception propagation。
- Overlay model 驗證多次 poll 不推進 tick、不新增 trace、不評估 invariant，Reset 清掉舊資料。
- .NET checker 同步引用新 module，驗證既有 gameplay 與 artifact 重跑不退化。
- 首次 Game View 檢查發現小視窗 HUD 重疊，已改為右側自適應面板與互斥顯示舊 HUD。
- 最終 Unity EditMode 再跑仍為 85/85 passed；.NET checker 加入唯讀 consumer 與既有 failure-example.json 相容性檢查，全部通過。
- Game View 已目視確認修正後的右側面板、角色 Observation、invariant PASS/tick 與增量 trace。注入 F3 後確認可隱藏面板。
- 驗收後移除測試鍵盤與暫存截圖，恢復原本 Edit Mode；沒有修改場景 YAML。

## 限制

單執行緒 in-process；generic trace payload 必須不可變。來源／面板皆按筆數限容量，不限制任意字串長度。
Stream identity 屬於診斷資料，不進 GameplayStateHash，也不代表 Replay input sequence。
面板只有 readonly 工程介面；沒有 transport、安全 sandbox 或 Release 遠端控制能力。
