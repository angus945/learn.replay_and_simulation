# 共用 scenario 驗收矩陣

此表定義本輪 framework / game 主線的驗收，不把 Protocol transport 或舊 physics TODO 當作已完成能力。當次執行結果見 [實作進度](../implementation-progress.md)。

| 情境 | 必須保留的契約 | 可執行證據 |
| --- | --- | --- |
| enqueue / 同 tick 多輸入 | 入列不改 state；target tick 到期後按 sequence 執行 | GameplayBehaviorTests、ModernGameplayContractChecks、教學 04 |
| 非零 / 正負斜向 / Stop | Domain 方向限制；Demo / manual 用相同規則；render 不回寫 state | ModernGameplayContractChecks、教學 01 / 05 |
| Move + Attack / range boundary | 同 tick 攻擊採位移前位置；先改方向不提前移動 | 現行 game 行為測試、DemoTemplateChecks、教學 05 |
| death / respawn / spawn budget | 死亡立即拒絕後續 action；commit 後解除活動身分；新 ID 不重用 | LifecycleAndRandomTests、GameplayPresentationTests |
| seed / RNG stream / delayed spawn | 血量與延遲獨立 stream；同 seed、同 inputs 重現 lifecycle / hash | LifecycleAndRandomTests、教學 05 |
| unknown / stale / duplicate / capacity | 結構化 admission / execution 結果；不靜默丟失已接收 inputs | TemplateContractChecks、ToolControlTests、ModernGameplayContractChecks |
| Reset / Stop / Fault | 新 session 身分；舊 cursor 不混流；未執行輸入取消；不能續跑 partial tick | TemplateContractChecks、SessionTemplateContractChecks、現行 game 行為測試 |
| custom oracle / recording | 每 session 獨立 invariant；policy 不符在 tick 0 拒絕；首次 failure 可重現 | ModernGameplayContractChecks、教學 05、CLI capture / rerun |
| callback reentry / wave cap | handler 可 enqueue 下一 wave，不能重入 dispatch / Clear；失敗清 queue | WaveDispatcherContractChecks、CoreHardeningContractChecks |
| 多 participant / phase reaction | 註冊順序固定；同 phase participants 後 drain；event-only reaction 不遺失 | CoreHardeningContractChecks、PhaseObservationTests |
| pool / multi actor presentation | stable ID 與 instance generation 分離；spawn snap / despawn / reuse；session 切換明確 snap | ActorAdapterTests、GameplayPresentationTests |
| local physics sensors | 只手動推進隔離 physics scene；facts 排序去重；溢位拒絕部分批次；外部 / unbound callback 拒絕 | LocalPhysicsTests |
| frame / playback | 30 / 144 FPS 與長 frame 同逐 tick 結果；Replay 不推進 live；JSON round trip | DemoTemplateChecks、GameplayPresentationTests、教學 05 |
| scene / player | 正式 Demo 為 build entry；匯入資產無 Missing Script；可啟動 Player | Editor 資產 audit、Play smoke、Windows build（結果記於實作進度） |

## 現行契約與刻意停止支援的格式

- 舊 GameplaySession／ports／artifact reader 已移除；教學、Demo、CLI、測試與 Protocol adapter 都使用現行 template runtime。
- 現行 generic trace 使用 `Stage=Phase, Type=phase, Code=begin/end`；input / event 的 Type 是 project metadata，不保證等於 CLR 型別名稱。
- 發生 tick 例外後，Observe 是最近已捕捉的 snapshot；DiagnosticSnapshot.ObservationTick 表示來源 tick。沒有 partial-world inspection / rollback 承諾。
- scenario / CLI 硬上限是各 100,000 ticks / inputs、65,536 trace entries；舊超限配置拒絕，不截斷。顯式 TemplateLimits 會隨現行 recording 保存。
- 現行只讀 TemplateRecording。舊 hash projection／ReplayArtifact／FailureArtifact 不再受目前版本支援；原始歷史檔案保留，不自動改寫。
- Protocol 核心 envelope v1 保持不變，game payload 改為 v2；adapter 不再投影舊 game payload v1。Transport 仍 Deferred。
- Unity sensor relay 接收 Enter / Stay，**不接 Exit**。Unity 延遲 Exit 沒有原 binding generation，不能在 pool reuse 後安全辨識。Despawn 由 lifecycle snapshot 表達。

## 退役判定

主線不再依賴 `Old_Simulation`。舊 Unity 資產已連 metadata 封存在該目錄的 `LegacyUnityAssets`；原本的 Missing Script 不再留在正式 Assets。

舊 GameplaySession／artifact API 與 CLI 舊路由已退役；Protocol consumer 已改接 modern ports。需要原工具或舊檔解讀時使用歷史基準 `22f6966`，現行不新增 legacy assembly／自動轉換器；詳見 [退休政策](../legacy-compatibility-retirement.md)。必要的 gameplay／RNG／診斷情境由 [game 測試](../../Assets/game/gameplay-simulation/tests/)及 [Overlay 測試](../../Assets/game/debug-overlay/tests/)繼續驗證，不因舊型別刪除而刪除行為需求。

不在本輪退出條件內：dynamic Rigidbody authority、physics outcome recording、cross-platform bitwise determinism、snapshot restore / rollback、Explorer、transport。這些不是可由目前 PASS 數量推導出的能力。
