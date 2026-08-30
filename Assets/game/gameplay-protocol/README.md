# Gameplay Protocol 專案 adapter

純 C# `Game.GameplayProtocol` 把 protocol DTO 直接映射到 `GameplayDefinition` 建立的 `TestableSimulationSession` 與 gameplay／simulation／results／diagnostics／admin ports。Move、Attack、Action Catalog 留在 game adapter；framework protocol 只負責 envelope、權限、控制租約、排程與 request retry。

本次是 **game payload v2 的 breaking change**，protocol **envelope 仍為 v1**。沒有舊 gameplay façade 或 v1 payload 相容分支，也沒有另建 runtime。此 adapter 目前只提供 in-process JSON boundary；尚未接 transport、authentication、Unity pump 或 reconnect。

## 版本與資料邊界

- 每個操作的 `PayloadJson` 都必須包含 `"Version":2`。無參數操作使用 `{"Version":2}`。
- 唯一例外是 `capabilities.read` 可用 `{}` 作初次 discovery；回應 `Version=2`。有指定版本時仍必須是 2。
- 缺少 `Version` 或必要欄位回 `payload.invalid`；合法 JSON 的其他 payload version 回 `payload.version.unsupported`。Envelope version 不是 1 時由 framework 回 `version.unsupported`。拒絕不會取得控制權、保留 action sequence、推進 tick、stop 或 reset。
- Top-level 成功 payload 都回 `Version=2`。Protocol error 仍是 envelope error，不另包 game payload。
- DTO unknown fields 依 `DataContractJsonSerializer` 忽略。Reset payload 的任何 scenario 欄位都不會成為設定來源。
- Sequence、TargetTick、Actor、Target、tick、RNG state 與 trace sequence 使用十進位**字串**，避免 JavaScript 遺失整數精度。X／Y 為 float。

## 操作目錄

| Operation | 權限 | Control | 行為 |
|---|---|---|---|
| capabilities.read | Observe | 否 | 不需已知 session ID；回傳 policy、實際 limits、driver ownership、操作與 game Action Catalog |
| control.acquire | 任一 Act／Drive／Admin | 否 | 無 realtime driver 時領取 session 獨占控制租約 |
| control.release | controller | 是 | 釋放租約；host 已建立 realtime driver 時仍可釋放 |
| action.submit | Act | 是 | `GameplayInput` 與 session／sequence／target-tick 分開交給 gameplay port；回 Queued／Code |
| simulation.step | Drive | 是 | 交給 simulation port 推進一 tick；回 Policy／ModernHash／Results |
| observation.read | Observe | 否 | 獨立 DTO：actors、player ID、tick、RNG state、spawn count、pending respawn ticks |
| results.read | Observe | 否 | AfterIndex／MaxItems，最多 1024，完成順序分頁 |
| diagnostics.read | Observe | 否 | observation、attempted Tick、ObservationTick、LastCompletedTick、invariants 與 fault |
| trace.read | Observe | 否 | stream cursor 增量讀取，最多 256 |
| session.reset | Admin | 是 | 只用 trusted host factory 提供的 scenario；新 session identity，需要新租約 |
| session.stop | Admin | 是 | 停止，保留可讀 diagnostics |

除了 discovery，操作都需要目前 SessionId。`control.acquire` 的 framework route permission 是 None，實際 acquire 仍要求 Act／Drive／Admin 其中一種權限。

Host 可以在 adapter 建立後、client 已 acquire 後，甚至 request enqueue 後建立 realtime runner。Adapter 在**每次 handler 執行時**讀取 session 的 `HasRealtimeDriver`，拒絕 acquire、submit、step、reset、stop，錯誤為 `session.realtime`。這個值來自真正的 driver ownership，沒有 caller mode flag；pause 仍持有 ownership，dispose runner 才釋放。Observe、results、diagnostics、trace、capabilities 保持可讀。Adapter 不會取消或接管 host 的 runner。

Framework 先檢查權限／session／租約，因此沒有合法租約的 mutation 可能先得到 `control.required`。同一 RequestId 的相同 retry 回傳之前記住的結果，不重新檢查 runtime 或執行；所有新的操作與新 snapshot 查詢都使用新 RequestId。

## 現代 admission、hash 與預算

`ActionDto` 必須包含 Version／Sequence／TargetTick／Kind／Actor。Kind 為 Move 或 Attack；Target 省略視為 0。Move 將軸值正規化。Gameplay 會檢查兩種 action 的有限軸值、actor 與 target；排入佇列不代表 domain 已接受操作。

AdmissionDto 使用現代 session 原始代碼：

| Code | 意義 |
|---|---|
| queue.accepted | input 已凍結並排入指定 tick |
| session.not_running | session 已停止或 faulted |
| session.stale | session identity 不符；protocol 一般會先在 envelope 拒絕 |
| sequence.invalid_or_duplicate | sequence 為 0 或同 session 已使用 |
| tick.out_of_range | 非未來 tick，或超過實際 MaxTicks |
| input.capacity | 已達 session lifetime MaxInputs，不因執行完 input 而釋出 |
| input.payload_budget | scenario 與所有已接受 input 的累計 UTF-8 payload 預算不足 |
| input.invalid | framework codec／單筆 payload 檢查拒絕 |

Admission 拒絕仍是成功的 protocol 操作：`Success=true`，payload `Queued=false`。無法解析 DTO 或未知 action kind 是 protocol error。`actor.unknown`、`parameters.invalid`、`target.out_of_range` 等 gameplay 結果在 tick 執行後由 Step／results 回傳，不能誤當 admission code。順序由 target tick，再由 caller 的非零且唯一 sequence 決定，與 request ID 或到達次序不同。

Capabilities 直接讀 `session.Limits` 的 MaxTicks／MaxInputs／TraceCapacity／MaxPayloadBytes／MaxTotalPayloadBytes，不拿 scenario 預設預算冒充 host override。這些是 simulation payload／recording 限制；protocol ingress／request／response／retry history 另外受 host 傳入的 `ProtocolLimits` 限制。

`CapabilitiesDto.HashKind="modernHash"`；`StepDto.ModernHash` 是該次 `TemplateTick.Hash`，即 GameplayDefinition canonical state 的 SHA-256，並回傳當前 `Policy`。它不是舊 StateHash 格式，也不是 Observation DTO JSON 的 hash。應在相同 scenario、policy 與受支援 runtime 條件下比較，不宣稱跨 Unity／.NET／CPU bitwise 相同。若 tick 在 hash 前 fault，ModernHash 可為 null；此時應讀 diagnostics 的 tick 與 failure 資訊。超過 tick 預算的 step 回 `tick.budget` 並保留 session 的停止語義。

## Host 接線

```csharp
GameplayDefinition definition = new GameplayDefinition();
GameplayScenario configuredScenario = new GameplayScenario();
TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session =
    definition.CreateTestSession(configuredScenario);
GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(session, () => configuredScenario);
ProtocolClient tool = new ProtocolClient("test-tool",
    ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive);
Task<ProtocolResponse> request = adapter.Endpoint.Enqueue(tool,
    new ProtocolRequest(1, "claim-1", session.Id, "control.acquire", "{\"Version\":2}"));
adapter.Endpoint.Drain(16); // Session owner thread, between ticks.
ProtocolResponse response = request.GetAwaiter().GetResult();
```

Namespaces：GameplaySimulation、Testability.Templates、GameplayProtocol、GameplayProtocol.Game、System.Threading.Tasks。

Constructor 必須提供 `Func<GameplayScenario>`。Host 負責更新這個可信設定來源，包括自己直接 reset session 後的情境。Factory 只在新 reset 執行時呼叫；null 回 `reset.scenario_unavailable`。同 request retry 不再呼叫 factory，也不再次換 identity。Reset 成功的 response envelope 帶新 SessionId，舊租約不適用新 session，client 必須重新 acquire。

Adapter 不擁有 session 的 Dispose；host 負責停止 pump、釋放 realtime runner 與最後 dispose session。Enqueue 可從其他 thread 呼叫，Drain 必須在建立 endpoint 的 session owner thread、tick 之間執行。DTO 可變，但 ingress 只接 immutable JSON string；回應 DTO 不共用 session state。

Trace 第一次查詢使用 Version=2、StreamId=`00000000-0000-0000-0000-000000000000`、AfterSequence=`0`，並指定 MaxItems。回應 cursor 獨立於 action sequence，包含 StreamChanged／MissedCount／HasMore。

## 驗證與尚未完成的整合

`GameplayProtocolContractChecks.RunAll()` 可在無 NUnit、無 Unity 的 CLI 執行；10 個 NUnit wrappers 使用同一組實際 JSON boundary checks。保留原六個 regression 場景，補 payload／envelope 版本、trusted reset retry、實際 limit override、現代 admission／hash、host 動態取得與釋放 driver ownership（包含 enqueue 與 Drain 之間的變更）。

這些檢查不代替跨程序驗收。Network transport、可信 client provisioning／authentication、Unity Update pump、連線關閉／reconnect 與 history 回收政策仍是後續明確範圍，不因移除舊 runtime 就視為完成。
