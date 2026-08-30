# 步驟 1–3 驗收紀錄

> **歷史紀錄**：本文保留基準 `22f6966` 之前的決策、API 與測試結果，不是現行入門指南。GameplaySession／舊 artifact 已退役；目前請讀 [五章教學](../framework-guide/learning-path.md)、[控制與 rerun](control-and-rerun.md)及[退休政策](../legacy-compatibility-retirement.md)。failure-example.json 原樣保留，不交給現行 rerun。

日期：2026-08-30。範圍：in-process 單實例，Move → Attack → Damage → Death。
對照 testability-develop-baseline-phase1-5.md；這不是宣稱 Phase 1–5 全部完成。

## Decision Log

- simulation 繼續擁有 tick/phase/wave/structural commit；testability 不另建 dispatcher 或第二套 loop。
- typed GameplayRequest 是專案行為資料，映射成 IIntent，內部工作使用 IInternalCommand，事後事實使用 IDomainEvent。
- 最小 testability 框架抽出 lifecycle/result/observer/invariant/trace/hash/codec；暫不為單一 observation 建 discovery registry。
- GameplaySession 是跨 Movement/Combat 的 project composition，不把遊戲語意放進 module。
- 初版固定傷害，不需要 RNG。RNG module 既有測試保留；stream policy 的實際整合留待需要隨機行為時。
- Reset 重建所有 session-owned 狀態，不嘗試倒轉現有 runner。失敗不可繼續推進，避免部分狀態再執行。
- 在 init 配發兩個穩定 ID；死亡 commit 後 registry 移除、observation 保留 tombstone。暫無 runtime spawn action。
- Protocol、Transport、通用 Snapshot Restore、智慧探索與 Orchestrator 未實作。

## Experiment Log／Test Evidence

- 初輪 Unity EditMode：63/63 passed，涵蓋原 41 項與本輪控制面／戰鬥／診斷測試。
- .NET 9 target（本機相容 SDK）不載入 Unity assembly，通過死亡移除、排序無關的逐 tick hash、JSON artifact 失敗重跑。
- 自測 oracle 使用刻意不合法的 observation，無 reflection 改 private field。
- 自訂 position invariant 驗證 non-crash failure capture；合法 rejection 不會建立 failure artifact。
- 有限但會在位移運算溢出的 scenario（float.MaxValue speed、2s tick）驗證 exception、Faulted、序列保存及重跑。
- 最終 Unity EditMode：69/69 passed（原 41 項 + 本輪 28 項）。.NET headless checks 再次通過。
- 空白鍵最初的 wasPressedThisFrame 測試失敗，改為 session-owned press-edge tracking 後通過；按住或補跑 tick 不重複攻擊。
- Game View 已確認角色、紅色敵人、HP 與 tick 顯示。鍵盤／死亡流程以自動測試驗證；最終互動驗收中 Editor 回到 Edit Mode，未宣稱完成三次現場攻擊驗收。
- failure-example.json 是本輪 .NET checker 實際產出的 JSON，包含 build/working-tree label、runtime、輸入與診斷；不是手寫示意資料。

## Risks／Known Limitations

- 同 runtime 精確浮點比較，沒有跨平台／版本保證；Physics／async/network 未整合。
- 外部輸入使用 caller-assigned target tick 與 sequence；不同鍵盤 frame 排程不保證把真實事件分到同一 tick。
- 所有控制面是單執行緒；長時間 hang 要靠外部 watchdog，当前預算只限制可返回的 tick 與資料容量。
- ActionResult Accepted 表示 action 已套用，不代表整個 tick 成功；tick 失敗可能保留部分效果。
- trace 有容量上限，較早 detail 可能丟失；完整 admitted action history 與 hash history 受 session 預算限制並保留。
- 未到期 queue 不進 GameplayStateHash；精確重跑還需要 scenario + 外部輸入序列，hash 不是 runtime snapshot。
- 自訂 invariant 的實作不在 artifact 內，重跑需要相同 build/composition；VerifyFailure 不會把缺少 policy 誤判為成功。
- Gameplay-only interface 是工程邊界，不是安全授權層。遠端權限與 Release 開关留在 Phase 4。

## 下一個 gate

擴充其他 gameplay 或物理前，維持相同正式行為入口、結果、observation 與 invariant 驗收。
若下一步轉向 Protocol，先檢視此最小 API 是否已滿足實際 Client 的 discovery、capability 與版本需求，不直接暴露 Domain entities。
