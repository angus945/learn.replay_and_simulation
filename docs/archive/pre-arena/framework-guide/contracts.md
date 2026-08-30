# 契約與專案策略

[回到索引](README.md)

## Framework 本身的契約

- Pipeline 先 Register，再 Seal；執行期間不能修改註冊。participant 依註冊順序執行。
- Intent／Internal Command 每個訊息型別只有一個 handler；缺少 handler 是錯誤。Domain Event 可有多個 handler，也可以沒有訂閱者。
- 三類訊息分開排隊；先 dispatch intents，再循環 drain commands、events，直到穩定或超過上限。
- 同 dispatcher 在處理期間新加入的訊息進入後續 wave；wave 不是整個 tick 的全域序號。
- 低階 SimulationRunner 保存第一次執行／render Failure，拒絕續跑，需重建 runner 與 pipeline；SimulationSession 另管理 Faulted／Reset，TestableSimulationSession 再保存可重播證據。都不承諾 rollback。
- phase begin/end callback 應唯讀且不丟例外；失敗 phase 不發出成功 end。
- 這些是使用契約，不是會攔截任意 C# setter 的執行沙箱。

## Phase 順序與目前專案映射

| 順序 | Framework phase     | 現行 GameplayDefinition／GameplayWorld                         |
| ---- | ------------------- | -------------------------------------------------------------- |
| 1    | IntentAcquisition   | TestableSimulationSession 已按 sequence 放入到期 inputs；沒有另註冊未錄製的 input source |
| 2    | IntentHandling      | 模板 InputIntent → InputCommand → GameplayDefinition.ExecuteInput → GameplayWorld.Execute／GameplayActions，再 drain events／reactions |
| 3    | PrePhysics          | GameplayWorld 更新 tick，GameplayActions.Advance 推進活著角色 |
| 4    | Physics             | 本遊戲未註冊 physics adapter；Unity framework 的 sensor adapter 是選配 |
| 5    | PostPhysics         | 預留，未註冊專案 participant                                   |
| 6    | StructuralCommit    | GameplayWorld 移除死者、同步 repository、安排／完成重生並檢查生命週期一致性 |
| 7    | PresentationCapture | 有接口，本遊戲未在 pipeline 註冊 presentation participant      |
| 之後 | Testability host    | CaptureObservation → canonical hash → invariants → 保存 TemplateTick；ActionResult 已於 input 執行完成時收集 |

PrePhysics 到 StructuralCommit 各 phase 的所有 participants 依註冊順序執行，再 drain reactions；不是每一個 participant 後立刻 drain。參考 [SimulationPipeline](../../Assets/framework.deterministic-simulation/src/API/SimulationPipeline.cs)與[GameplayWorld](../../Assets/game/gameplay-simulation/src/Runtime/GameplayWorld.cs)。

目前畫面 snapshot 在 realtime runner 的 tick 後 callback 經 MovementDemoSession → GameplayActorPresentation 捕捉，LateUpdate 再 Render；不是 pipeline 自動完成所有呈現。
PresentationRender 不屬於權威 tick。StructuralCommit 之後不應再改變本切片的權威生命週期。

## Testability 正式控制面契約

- 一個 session 只能由 Manual Step 或唯一 Realtime driver 推進，不混用；現行模板先 Dispose driver 才能手動 Step／Reset／Dispose session。
- Request 以 SessionId 隔離；Reset 換 identity。Sequence 非零且唯一，同 tick 按它排序，不按抵達順序。
- CurrentTick < TargetTick <= MaxTicks；Submit 只排隊。Queued 與 ActionResult.Accepted 不同。
- Gameplay port 不提供任意 SetHealth／Spawn／Reset；Admin 用 scenario 建立測試條件。
- Faulted 保存首次失敗，禁止繼續 Step；LastCompletedTick 與嘗試中的 CurrentTick 不同。
- Stop／Fault 取消尚未執行的外部 action，但不捏造其執行結果；Reset 清世界。
- Diagnostics 讀取不重新 Evaluate、不 Step、不寫 trace；snapshot 為唯讀複本。
- InvariantReport 帶評估 tick，consumer 要區分尚未評估與舊結果。

這些能力由 [TestableSimulationSession](../../Assets/framework.testability/src/API/TestableSimulationSession.cs)提供，不是低階 Pipeline 自帶功能。工具直接使用模板 ports，已沒有 GameplaySession 舊 ports 轉接層。現行使用方式見[第 4 章](../../tools/gameplay-lessons/lessons/04-testability.md)。

## 本專案的選擇，不是框架硬規定

- Move 更新持續方向；Attack 在位移前檢查距離。
- 先提交 destroy、清 repository，再提交 spawn；兩次 registry commit 不是原子交易。
- 新敵人當 tick commit 後可見，下個 tick 才能被操作。
- 死者 observation 保留 tombstone，以生成上限約束數量。
- Demo 的位置、血量 20..40、延遲 1..3 秒、最多 128 隻都是專案政策。
- Session、callbacks 單執行緒；Protocol ingress 的背景排隊不代表 domain 可跨 thread 存取。

## 決定性與保存邊界

現行 TemplateReplay 重建 scenario／seed，重送外部輸入，不把內部 command/event 當錄製輸入。[GameplayDefinition.EncodeCanonicalState](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)固定寫入其 schema marker、tick、兩條 RNG state、出生數、待重生 ticks 及依 ID 排序的 actor state；PolicyId 識別規則／codec／hash／invariant 政策。

舊 GameplayStateHasher 的 hash layout 1／2／3 與 ReplayArtifact／FailureArtifact reader 已退役；TemplateRecording 不接受這些舊格式，也不自動重新計算舊 hash 來冒充相容。原始歷史檔案與工具基準見 [退休政策](../legacy-compatibility-retirement.md)。
Hash 不含 session GUID、畫面插值、trace 或未來外部 request queue，因此不是完整 runtime snapshot。
目前只以相同 runtime／規則比較；SHA-256 不會讓浮點或 Unity physics 自動跨平台決定性。
更動規則需評估 policy/schema/build 相容性，不可只更新預期 hash 就宣稱舊 replay 相容。

Protocol 核心 envelope v1 與 game adapter payload v2 是不同契約；adapter 直接映射現行 ports／結果，不保留 v1 game payload 投影。**Transport 仍 Deferred**：只有 in-process JSON boundary，沒有認證實作、durable exactly-once 或外部 client。不要把 protocol ok 當作 action 成功。
