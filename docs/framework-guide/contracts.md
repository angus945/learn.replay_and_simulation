# 契約與專案策略

[回到索引](README.md)

## Framework 本身的契約

- Pipeline 先 Register，再 Seal；執行期間不能修改註冊。participant 依註冊順序執行。
- Intent／Internal Command 每個訊息型別只有一個 handler；缺少 handler 是錯誤。Domain Event 可有多個 handler，也可以沒有訂閱者。
- 三類訊息分開排隊；先 dispatch intents，再循環 drain commands、events，直到穩定或超過上限。
- 同 dispatcher 在處理期間新加入的訊息進入後續 wave；wave 不是整個 tick 的全域序號。
- 執行 exception 向上傳遞，framework 不承諾 rollback。Generic runner 本身不是 Faulted session 管理器。
- phase begin/end callback 應唯讀且不丟例外；失敗 phase 不發出成功 end。
- 這些是使用契約，不是會攔截任意 C# setter 的執行沙箱。

## Phase 順序與目前專案映射

| 順序 | Framework phase     | 現有 GameplaySession                                           |
| ---- | ------------------- | -------------------------------------------------------------- |
| 1    | IntentAcquisition   | Session 已先放入指定 tick 的 requests；沒有另註冊 input source |
| 2    | IntentHandling      | RequestIntent → ExecuteAction → domain event／reaction         |
| 3    | PrePhysics          | 活著角色移動                                                   |
| 4    | Physics             | 預留，未接 physics adapter                                     |
| 5    | PostPhysics         | 預留，未註冊專案 participant                                   |
| 6    | StructuralCommit    | 移除死者、安排／完成敵人重生                                   |
| 7    | PresentationCapture | 有接口，目前未由 GameplaySession 註冊                          |
| 之後 | 專案自訂            | hash、生命週期檢查、invariants、結果保存                       |

目前畫面 snapshot／插值在 MovementDemoSession 的 tick 後處理，不是 pipeline 已自動完成所有呈現。
PresentationRender 不屬於權威 tick。StructuralCommit 之後不應再改變本切片的權威生命週期。

## GameplaySession 控制面契約（不是 generic runner 自帶功能）

- 一個 session 只能由 Manual Step 或唯一 Realtime driver 推進，不混用。
- Request 以 SessionId 隔離；Reset 換 identity。Sequence 非零且唯一，同 tick 按它排序，不按抵達順序。
- CurrentTick < TargetTick <= MaxTicks；Submit 只排隊。Queued 與 ActionResult.Accepted 不同。
- Gameplay port 不提供任意 SetHealth／Spawn／Reset；Admin 用 scenario 建立測試條件。
- Faulted 保存首次失敗，禁止繼續 Step；LastCompletedTick 與嘗試中的 CurrentTick 不同。
- Stop／Fault 取消尚未執行的外部 action，但不捏造其執行結果；Reset 清世界。
- Diagnostics 讀取不重新 Evaluate、不 Step、不寫 trace；snapshot 為唯讀複本。
- InvariantReport 帶評估 tick，consumer 要區分尚未評估與舊結果。

## 本專案的選擇，不是框架硬規定

- Move 更新持續方向；Attack 在位移前檢查距離。
- 先提交 destroy、清 repository，再提交 spawn；兩次 registry commit 不是原子交易。
- 新敵人當 tick commit 後可見，下個 tick 才能被操作。
- 死者 observation 保留 tombstone，以生成上限約束數量。
- Demo 的位置、血量 20..40、延遲 1..3 秒、最多 128 隻都是專案政策。
- Session、callbacks 單執行緒；Protocol ingress 的背景排隊不代表 domain 可跨 thread 存取。

## 決定性與保存邊界

Replay 重建 scenario／seed，重送外部輸入，不重播內部 command/event。
Hash schema 1 是舊玩法，2 加入生命週期／血量 RNG，3 加入延遲 RNG 與待重生 ticks；未啟用新功能的舊 scenario 保持舊 layout。
Hash 不含 session GUID、畫面插值、trace 或未來外部 request queue，因此不是完整 runtime snapshot。
目前只以相同 runtime／規則比較；SHA-256 不會讓浮點或 Unity physics 自動跨平台決定性。
更動規則需評估 policy/schema/build 相容性，不可只更新預期 hash 就宣稱舊 replay 相容。

Protocol 核心目前只提供 in-process JSON boundary；沒有 transport、認證實作、durable exactly-once 或外部 client。不要把 protocol ok 當作 action 成功。
