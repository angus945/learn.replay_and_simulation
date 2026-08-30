# Testability：in-process 基礎

新專案使用方式請先讀 [Arena DDD／Clean Architecture 教材](../../docs/arena-guide/README.md)。

**可繼承模板：** [ReplayableSimulationDefinition 接線](../../docs/arena-guide/04-input.md)。模板提供通用控制面、診斷、記錄、正常／失敗 Replay，Domain 不需繼承框架。
本 assembly 依賴 Framework.DeterministicSimulation（單向）；基本 simulation 不反向依賴 testability。正式工具使用 ITemplateGameplay／ITemplateSimulation／ITemplateAdmin／ITemplateResults 與 IDiagnosticReader。

本框架提供跨專案機制，不提供遊戲規則，也不另建 simulation loop 或 Action dispatcher。

- `src/API`：Session lifecycle、唯讀 observer、IDiagnosticReader。
- `src/Contract`：Session state、SubmissionResult、ActionResult、simulation TraceEntry、DiagnosticSnapshot／InvariantReport。
- `src/Runtime`：simulation trace adapter、SHA-256 digest、JSON artifact stream codec。
- `tests`：鎖定、重複檢查、穩定排序、容量與序列化測試。

## Contract

SubmissionResult.Queued 只表示已排隊；ActionResult 才描述執行時的 Accepted／Rejected／InvalidRequest／Failed。
Gameplay 拒絕不視為 exception，InvariantViolation 與 exception 分開保存。
InvariantRegistry／IInvariant／InvariantViolation 已抽到 module.invariant-checks（InvariantChecks namespace）。
TraceRecorder 組合 module.trace-buffer 的 TraceBuffer<TraceEntry>，不再自行維護 ring buffer。
TraceRecorder.Reader 與 Writer 是不同 facade；Snapshot 用於錄製有限 trace 證據，互動工具應用 Reader 的增量 Read。
TraceEntry 提供 session、tick、sequence、stage、type、code、wave、actor、target；未適用的 identity 為零。
TraceEntry.Sequence 是 action correlation；TraceRecord.Sequence 才是增量讀取位置，兩者不能混用。
IDiagnosticReader 只查詢 snapshot 與 trace，不提供 Evaluate／Step／Submit／Reset。
InvariantReport 保留最近一次完成評估的 tick；讀取時不重新執行規則，consumer 必須辨別 NOT EVALUATED 與 STALE。
StateDigest 只處理 bytes，欄位與排序語意由專案的 hasher 決定。
ArtifactJson 不擁有 stream，不會關閉呼叫者的 stream；儲存位置、容量與 schema 驗證由呼叫端負責。

Session 與所有 callbacks 採單執行緒；不提供 thread-safety 或對惡意 callback 的強制中斷。
本 assembly 不提供 Protocol transport、多程序 runner 或通用 snapshot restore；Protocol 核心位於獨立 framework。
Observation 目前以 typed port 提供；通用 discovery/ObservationRegistry 等到有多種觀察模型需求再抽取。

## Definition 接點與診斷因果

`ExecuteInput(world, input, InputExecutionContext context)` 提供本次輸入的 SessionId、Sequence、TargetTick 與 Events。
Integration／Application 可以將 Sequence 放進自己的事件 envelope，再由 `DescribeMessage(object message)` 映射診斷資訊；Domain aggregate 不需要依賴 context 或實作 trace 介面。
既有 `ExecuteInput(world, input, IDomainEventSink events)` override 仍可用。Definition 必須實作其中一個 overload。

- `DescribeInput(input)`：回傳 `TemplateTraceMetadata`，提供穩定 Type、Actor、Target。框架一律使用實際 input envelope 的 Sequence，不接受描述取代排序鍵。
- `DescribeMessage(message)`：為專案 event／internal command 提供 Type、Sequence、Actor、Target、Detail。只有專案知道的因果關係才填 Sequence；無因果關係填零，不能借用上一個 action。
- Metadata 是不可變描述，不參與 routing、不變成 replay input，也不改 gameplay state hash。Type 上限 256 字元、Detail 上限 4096 字元。
- Admission／Action trace 的 Type 是 input 描述、Code 是 queue／execution reason；dispatch trace 的 Stage 是 Intent／InternalCommand／DomainEvent、Type 是訊息描述、Code 是 Detail。
- StateHash trace 記錄 checkpoint；Invariant trace 記錄違反規則。讀取 diagnostics 只回已完成的 snapshot，不重新評估。

錄製只保存外部 encoded inputs。重播重新執行 Application／Domain，從重新產生的事件取得 trace；不把 recorded event 再送入 dispatcher。
事件 handler 失敗時，可保存導致該事件的 input sequence；已完成的 ActionResult 保持 Accepted，並另外標記 session fault，沒有 rollback。

## Run policy 與結果分頁

`CreateDefaultLimits(scenario)` 可讓專案由唯一的 run configuration 建立 TemplateLimits，避免 game scenario 與 framework 的預設預算不同。
省略 limits 時，每次 Reset 都依新 scenario 重新取得 defaults；候選 limits 與世界初始化成功後才一起替換，失敗仍保留原本世界與預算。明確傳入 `CreateTestSession(scenario, limits)` 時，以該 limits 為準，Reset 也保留此覆寫。Replay 使用 recording 內的 limits；不重新套用目前的 defaults。
Session 的 `Policy`、`Limits`、`InvariantReport` 為唯讀；InvariantReport 是最近一次完成評估的證據，tick 0 尚未評估。

`Results.Read(sessionId, afterIndex, maxItems)` 依完成順序回傳 `TemplateActionResultPage`：Items、NextIndex、HasMore。
afterIndex 是已讀結果筆數，不是 action sequence；單頁 1–1024 筆。Page 擁有自己的不可變集合，後續完成不會改動已取得的頁面。
Reset 清除結果並更換 session ID；舊 cursor 明確拒絕。Stop／Fault 的未來輸入保持 Cancelled，不偽造 completed result。

PolicyId 必須包含 gameplay／codec／hash／invariant composition 版本。Replay 逐字比較 PolicyId，policy 不符時停在 tick 0；Runtime 不同只給 warning。
TemplateRecording 的 schema 1 是現行格式；trace metadata 是診斷內容，不構成新的 executable schema。不能只看數字 1 就把其他 artifact 當作此格式。Arena 不相容舊 game recording；ArtifactJson 是通用 stream codec，並不是舊格式自動轉換器。

## 契約驗證

`TemplateContractChecks` 的純 C# checks 由 Unity NUnit wrapper 與 headless host 共用。
新增檢查涵蓋 immutable input 描述、跨 event dispatch 的 causation、失敗後 Accepted result 保留、結果分頁／Reset、預設 run limits、policy mismatch 不推進、replay 候選世界 setup 失敗清理與重新 Restart。
這些是 in-process 契約驗證，不代表 Protocol transport、Explorer 或跨平台 determinism 已完成。
