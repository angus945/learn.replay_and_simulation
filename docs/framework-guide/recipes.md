# 功能食譜

[回到索引](README.md)。以下是開發順序與驗收條件，不是要求所有功能都有同一份樣板。

## 1. 新增 gameplay action

情境：加入互動、技能或移動操作。

1. 先寫 domain 規則與測試：成功會改哪些狀態？失敗會不會改狀態？
2. 定義外部 request 的資料，不傳 aggregate 或 Unity object 引用。
3. 把 session／sequence／tick 驗證放 admission；把目標活性、距離、資源等放執行時驗證。
4. 對完整控制面，依 RequestIntent → Internal Command → application/domain → ActionResult 組裝。
5. 只有發生需要其他流程反應的事實才發 event。
6. 更新 capabilities／action catalog、observation、hash 與 replay 驗證；若已有 protocol route，再更新 DTO 映射。

參考 [GameplaySession handlers](../../Assets/game/gameplay-simulation/src/Runtime/GameplaySession.cs)及[操作測試](../../Assets/game/gameplay-simulation/tests/GameplaySessionTests.cs)。
最低驗收：成功、非法參數、業務拒絕、重複 sequence、舊 session、同 tick 順序。
常見錯誤：Submit 就扣血；測試直接呼叫 setter 繞過正式操作；把業務拒絕當 exception。

## 2. 跨領域事件與 reaction

情境：戰鬥死亡需要停止移動、移除物件。

1. 確定哪個 aggregate 擁有事實：例如 Combatant 的 HP 已歸零。
2. 協調層取得結果，映射成 ActorDied 等事件，不要求 domain 引用 dispatcher。
3. Event handler 轉成後續工作／Internal Command；不要把尚未成功的請求命名成過去式事件。
4. 明確定義同 tick 後續操作能否看見死亡；目前 dead flag 即刻生效，registry 移除延至 commit。
5. 測同 tick 重複死亡／攻擊、事件沒有 subscriber，以及 reaction 上限。

參考 [MessagePipeline](../../Assets/framework.deterministic-simulation/src/Runtime/MessagePipeline.cs)。
常見錯誤：以為每個 command 的 event 都會立即插入下一個 command 前；目前是先 drain commands 再 drain events。

## 3. 生成與銷毀

情境：敵人出生、死亡、召喚物到期。

1. Domain 判斷是否死亡／是否允許生成。
2. Integration 向 registry 提出請求，由專案選定 structural boundary。
3. 在 commit 同步 registry 與相關 repositories；不要在遍歷 active collection 時任意直接移除。
4. 定義何時可以操作新物件，以及 stale ID／handle 的回應。
5. Unity view 依已提交 observation 綁定／解除綁定，不讓 Destroy(GameObject) 代表 domain 已死亡。
6. 設定 active／保留資料的容量與清理策略；generation 重用不代表 gameplay ID 重用。

參考 [registry 契約](../../Assets/modules/module.simulation-object-registry/README.md)及[生命週期測試](../../Assets/game/gameplay-simulation/tests/LifecycleAndRandomTests.cs)。
最低驗收：出生前不可操作、死亡後不可操作、重複 destroy、slot 重用、repository 一致性、預算耗盡。
常見錯誤：假設兩次 Commit 是原子交易；把 tombstone 當 active entity。

## 4. 新增 RNG 與延遲行為

情境：隨機血量、掉落或重生時間。

1. 為規則指定穩定 stream ID；記錄用途，避免重複占用。
2. 在明確 gameplay 事件抽一次；不要在 Observe／Render 或每 frame 重抽。
3. Scenario 保存 seed／規則設定，Reset 重建 RNG。
4. 計時使用 tick 排程，明確定義取整、上下界與到期 phase。
5. 把影響未來行為的 RNG state／待執行排程納入專案 hash；不宣称 hash 是完整 snapshot。
6. 規則變更時決定 schema／policy 相容策略。

目前例子：health stream=1；respawn stream=2；延遲在 [ceil(1/TickDelta), floor(3/TickDelta)] 抽 tick 數，到期 StructuralCommit 生成。
Bounded integer 的 rejection sampling 可能消耗多個原始 RNG draw，不假定一次 NextInt 等於一次 UInt64 draw。
參考 [seeded-random](../../Assets/modules/module.seeded-random/README.md)與[實際重生規格](../testability/simulation-lifecycle-phase-random.md)。
最低驗收：相同 seed 重現、不同 seed 有變化、無效操作不多抽、Reset、等待期間不重抽、生成上限、JSON replay。

## 5. Observation／Invariant／Trace

情境：新功能需要 debug overlay 與自動測試讀取。

1. Observation 複製必要資料，包含判斷未來排程所需的診斷資訊；不要暴露可變 aggregate。
2. Domain 自己維持的規則先放 domain；跨資料結構一致性可在 tick 結束額外檢查。
3. Invariant 經正式評估流程執行，保存評估 tick；讀取不重跑 checks。
4. Trace 記錄相關 session／tick／action sequence；增量 cursor 用 trace record sequence，不混用 action sequence。
5. 規則／trace 有容量上限，工具顯示 gap、stale、not evaluated，不以缺少資料冒充成功。

參考 [Testability](../../Assets/framework.testability/README.md)與[Overlay tests](../../Assets/game/debug-overlay/tests/DiagnosticReaderTests.cs)。
最低驗收：多次 Poll 不改 hash/tick、不增加 trace；Reset stream 切換；旧 snapshot 不隨世界改變。

## 6. Unity 呈現 adapter

先用純 C# 完成玩法，再接 Unity 輸入與 view。權威 domain 位置轉成 Transform，不反向讀 Transform 決定攻擊距離。
前後 tick 插值屬 presentation，不參與 gameplay hash；spawn／destroy、跳 tick、暫停需明確 snap 或清除舊插值資料。
參考 [MovementDemoHost](../../Assets/game/movement-demo/src/Unity/MovementDemoHost.cs)。
最低驗收：低 FPS 補 tick 不重複消耗按鍵邊緣；新敵人不顯示成死者；Replay 模式不注入即時玩家輸入。
