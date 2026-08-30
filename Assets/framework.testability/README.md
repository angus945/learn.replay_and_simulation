# Testability：in-process 基礎

新專案使用方式請先讀 [DDD 遊戲框架開發指引](../../docs/framework-guide/README.md)。

**可繼承模板：** [ReplayableSimulationDefinition 使用指南](../../docs/framework-guide/testability-replay-template.md)。模板提供通用控制面、診斷、記錄、正常／失敗 Replay，Domain 不需繼承框架。
本 assembly 現在依賴 Framework.DeterministicSimulation（單向）；基本 simulation 不反向依賴 testability。既有 typed ports 與 codec 仍保留。

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
TraceRecorder.Reader 與 Writer 是不同 facade；Snapshot 為 artifact 相容性保留，工具應用 Reader 的增量 Read。
TraceEntry 提供 session、tick、sequence、stage、type、code、wave、actor、target；未適用的 identity 為零。
TraceEntry.Sequence 是 action correlation；TraceRecord.Sequence 才是增量讀取位置，兩者不能混用。
IDiagnosticReader 只查詢 snapshot 與 trace，不提供 Evaluate／Step／Submit／Reset。
InvariantReport 保留最近一次完成評估的 tick；讀取時不重新執行規則，consumer 必須辨別 NOT EVALUATED 與 STALE。
StateDigest 只處理 bytes，欄位與排序語意由專案的 hasher 決定。
ArtifactJson 不擁有 stream，不會關閉呼叫者的 stream；儲存位置、容量與 schema 驗證由呼叫端負責。

Session 與所有 callbacks 採單執行緒；不提供 thread-safety 或對惡意 callback 的強制中斷。
本 assembly 不提供 Protocol transport、多程序 runner 或通用 snapshot restore；Protocol 核心位於獨立 framework。
Observation 目前以 typed port 提供；通用 discovery/ObservationRegistry 等到有多種觀察模型需求再抽取。
