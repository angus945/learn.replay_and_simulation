# 能力與驗收清單

[教材索引](README.md)

此清單回答「新 Arena 示範接了哪些能力、為何需要、由哪裡驗證」。它不是測試結果報告，沒有因勾選、檔案存在或文件說明而推定通過。CLI selector、framework contracts、NUnit、PlayMode、Player 各自提供不同證據。

## DDD／Clean Architecture

- Aggregate 本地一致性：Actor 同時保護血量、死亡與方向；Position 拒絕非有限值。見 [01](01-domain.md)、[Actor](../../Assets/game/arena/src/Domain/Actor.cs)、[Domain tests](../../Assets/game/arena/tests/Domain/ActorTests.cs)。執行 `domain`。
- 用例與外部能力分離：Application 只依賴 Domain，向內定義 repository／lifecycle／random ports。見 [02](02-application.md)、[Ports](../../Assets/game/arena/src/Application/Ports.cs)、[Application tests](../../Assets/game/arena/tests/Application/ArenaApplicationTests.cs)。執行 `application`。
- 編譯依賴可驗證：Unity asmdef 與 `tools/arena-build` 的 ProjectReference 對應分層；不是把所有來源放進一個 executable。Domain／Application 不應取得 framework 或 Unity references。
- 同一 production composition：manual、live、recording、replay 都由 ArenaDefinition 建立；純 Domain/Application 測試可直接驗證內層，不代表正式 host 可繞過 Submit。

## DeterministicSimulation 接線

- World factory／cleanup／Reset：每個 session 建立獨立 repository、RNG、Application；反序列化得到的非法 scenario 在 Reset 入口拒絕，不破壞原 session。見 [03](03-simulation.md)、[ArenaRuntime](../../Assets/game/arena/src/Integration/ArenaRuntime.cs)、[ArenaDefinition](../../Assets/game/arena/src/Composition/ArenaDefinition.cs)。執行 `simulation`。
- 固定 phase：input → reactions → movement → structural commit；participants 按註冊順序，phase 後 drain。Arena 不自己重寫 dispatcher。見 [ArenaSimulationWiring](../../Assets/game/arena/src/Integration/ArenaSimulationWiring.cs)。
- 必要 handler 宣告：RequireCommand 與 Register 分開；缺少必要 handler 應初始化失敗。event 無 subscriber 本身合法，game 必須另驗證死亡／重生接線。
- 內部 command／event：純 ArenaFact 映射為 ArenaFactMessage；RespawnCommand 是內部工作，不重複錄製。見 [05](05-lifecycle.md)。執行 `lifecycle`。
- StructuralCommit：移除／到期出生後才產生對外活動清單；新 actor ID 不重用。registry 與 repository 一致性由 post-tick oracle 檢查。
- 確定性依賴：穩定 ID／ordered repository、獨立 RNG streams、tick-based due queue、出生預算。這些由 modules 與 Application 分工，不由 framework 猜遊戲政策。
- Realtime ownership／catch-up：只有 session factory 產生的 runner 驅動；Pause 不交出權限，callback 不重入，Dispose 順序明確。live input methods 拒絕非 owner／disposed 呼叫；Pause／Stopped／Faulted 顯示最新可用 snapshot，alpha=1。見 [09](09-realtime.md)。執行 `realtime`；低階極端錯誤契約另見 framework tests。

## Testability 接線

- 正式 input bridge：ArenaInput → 框架 Intent／Command → ExecuteInput → ArenaRequest。見 [04](04-input.md)。執行 `input`。
- Admission：identity、sequence、target tick、input/payload 容量；Queued 不等於 Accepted。
- 執行結果：成功、業務拒絕、非法參數分開；Find／Read 不從 trace 推測。Stop/Fault 取消未執行輸入，Reset 更換 identity。
- Observation：immutable snapshot、不洩漏 Actor；Observe／Diagnostics 不再推進或重算。見 [06](06-observation.md)。執行 `observation`。
- Canonical state：明確 schema、有序 actor、持續方向、不可變 ArenaRules／TickDelta、RNG、pending due ticks、ID／registry evidence；hash 由 framework 計算，非完整 restore checkpoint。
- Invariant／oracle：Domain invariant 與 post-tick oracle 不混用；每 session 建新 checks，training oracle 使用不同 policy。見 [07](07-diagnostics.md)。執行 `diagnostics`。
- Trace：外部 action causation、fact／command metadata、phase、lifecycle、bounded cursor；來源缺口與本地顯示淘汰分開。
- 首次 failure：attempted tick、LastCompletedTick、ObservationTick、results／hash 的邊界明確；不承諾 rollback 或故障後續跑。
- Recording／JSON：scenario／input codecs、實際 limits、完整 tick boundary、首次 failure；caller 擁有 stream、檔名與不覆寫責任。見 [08](08-replay.md)。執行 `capture`／`capture-failure`／`rerun`。
- Replay：從同一 Definition 建新世界，比對逐 tick results/hash/failure；Completed、Diverged、ReproducedFailure 分開。執行 `replay`，另以不同 frame schedules 驗證。
- Policy：明確 known-policy composition；未知 policy 不載入任意程式，不自動解讀舊 game recording。

## Unity 外圍整合

- Input System → TickInputBuffer → Submit：frame axes 與 press edge 分開，模式切換清 buffer。見 [10](10-unity.md)、[ArenaHost](../../Assets/game/arena/src/Unity/ArenaHost.cs)。
- Observation → ActorPose → UnityActorPresentation／Pool：stable game ID 與 instance generation 分開；死亡移除、出生 snap、catch-up pair、跨 session snap。
- 唯讀 diagnostics consumer：ArenaDiagnosticsPanel 只有 IDiagnosticReader，不取得 Step／Admin。
- Recording／Replay UI：保存新檔、載入、播放、pause、step、restart、return live；播放不推進原 live world。
- 需要另外執行 Unity 編譯、EditMode／PlayMode、scene smoke 與 Player build／startup。CLI 成功不能填補這些證據。

## 沒有接入、也沒有假裝支援的能力

- Physics／PostPhysics 是可用 phase，但 Arena 沒有物理 gameplay participant。local physics sensors 是 Unity framework 的選配 reference，不是已接碰撞傷害。
- Dynamic Rigidbody authority、physics outcome recording、跨平台 bitwise determinism：不在本例承諾內。
- Snapshot restore、rollback、任意 tick seek、完整逐欄位差異定位：未由 recording/hash 自動提供。
- Transport、authentication、Protocol adapter、自動探索／fuzzer、跨程序 watchdog：不是這兩 framework 接線教材的前置，也未在此新增。
- 舊 game API、玩法、recording／scenario 相容：不保留；框架自身契約測試仍需維持，不能隨舊 game 工具一起遺失。

## 交付時如何判定完成

1. `all` 與各 selector 可執行，錯誤有非零 exit；不只展示成功路徑。
2. 分層 .NET libraries 可單獨建置，architecture check 禁止 Domain/Application 反向引用外層。
3. 正常與 failure recording 可保存、讀回、重現；篡改 input/hash/policy 不得被當成功。
4. Unity 的 presentation／input／mode switching 另有測試與實際場景證據。
5. 本教材的檔案連結、命令、API 片段與 production source 一致；沒有脫離編譯來源的第二套 game。
6. 當次報告明確列出已執行及未執行層次，不沿用先前 game 的通過數字。
