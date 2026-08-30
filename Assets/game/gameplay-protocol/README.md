# Gameplay Protocol 專案 adapter

> **Deferred（暫緩）**：先穩定 deterministic-simulation、testability、game 接線與教學，之後才遷移／擴充本 adapter。保留既有 Protocol 程式、DTO 與必要相容入口；不新增 transport，也不以 Protocol 遷移完成阻擋其他階段。最新範圍見 [分階段實作進度](../../../docs/implementation-progress.md)。

純 C# Game.GameplayProtocol 引用 Framework.GameplayProtocol，將協定 DTO 映射到既有正式控制面。
這是本專案整合，不把 Move/Attack/Observation 塞進 framework。

下列範例仍使用 `GameplaySession` 相容 API；該 facade 現在轉接與 Demo 相同的 `GameplayDefinition → TestableSimulationSession` runtime，沒有另一份玩法。Protocol adapter 本身仍暫緩，尚未改為直接使用現行 ports，也不代表外部服務已完成。

由 host 先建立並 Start 一個 session，再建立 GameplayProtocolAdapter。
Manual session 供 Fuzzer／AI 操作；Realtime session 本版只供觀察，不搶玩家輸入／tick driver。
尚未自動接到 MovementDemoHost，沒有網路 listener。

## 操作目錄

| Operation | 權限 | Control | 行為 |
|---|---|---|---|
| capabilities.read | Observe | 否 | 無須已知 session ID；回傳版本、session、預算、操作／Action Catalog |
| control.acquire | 任一 Act/Drive/Admin | 否 | 領取 Manual session 獨占控制權 |
| control.release | controller | 是 | 釋放控制權 |
| action.submit | Act | 是 | DTO → GameplayRequest，只回 Queued／Code |
| simulation.step | Drive | 是 | 推進一個 tick，回傳 hash／ActionResults |
| observation.read | Observe | 否 | 專案 Observation DTO |
| results.read | Observe | 否 | AfterIndex／MaxItems，最多 1024，完成順序分頁 |
| diagnostics.read | Observe | 否 | observation、session state、invariant tick／violations／fault |
| trace.read | Observe | 否 | stream cursor 增量讀取，最多 256 |
| session.reset | Admin | 是 | 以當前已配置 scenario 重建；回應帶新 session ID，需要重新 acquire |
| session.stop | Admin | 是 | 停止，保留可讀 diagnostics |

除了 capabilities.read，都需要目前 SessionId。控制操作回空 JSON object；所有權限由 server 配發。
control.acquire 的 route permission 為 None，handler 另外檢查至少有一種 mutation 權限，並拒絕 Realtime。

ActionDto：Sequence／TargetTick／Actor／Target 使用十進位 **字串**，避免 JavaScript 丟失 ulong 精度；
Kind 為 Move 或 Attack，X/Y 為 float。Actor、Sequence、TargetTick、Kind 為必要欄位。
Target 預設 0；Move 軸值會正規化，Attack 的軸值不用來計算攻擊但仍由 gameplay 檢查有限性。
Action sequence 是 caller 指定的 gameplay 排序鍵，不是 request ID，也不是 transport arrival order。
JSON DTO unknown fields 依 DataContractJsonSerializer 忽略；必要欄位缺失回 payload.invalid。

Trace 第一次查詢：StreamId=`00000000-0000-0000-0000-000000000000`、AfterSequence=`0`。
回應 cursor 獨立於 action sequence；包含 StreamChanged／MissedCount／HasMore。
每次想取得新頁或更新後 snapshot 都使用新的 RequestId。

## In-process 使用範例

```csharp
GameplaySession session = new GameplaySession();
session.Start(new GameplayScenario());
GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(session);
ProtocolClient tool = new ProtocolClient("test-tool",
    ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive);
Task<ProtocolResponse> request = adapter.Endpoint.Enqueue(tool,
    new ProtocolRequest(1, "claim-1", session.Id, "control.acquire"));
adapter.Endpoint.Drain(16); // Owner thread, between ticks; future Unity host calls this from Update.
ProtocolResponse response = request.GetAwaiter().GetResult();
```

Namespaces：GameplaySimulation、GameplayProtocol、GameplayProtocol.Game、System.Threading.Tasks。
`ProtocolJson` 提供 JSON codec；DTO 對外可變，但 Enqueue 接收的是已編碼 immutable string，不攜帶共享 DTO 實例。
Codec 是 adapter 的 JSON 選擇，framework handler 仍只接收 envelope，沒有 domain 類型依賴。

## 暫緩後的後續範圍

其他 framework 與正式 game ports 穩定後，先處理 adapter／results／capabilities 的對接與相容性，再選本機 transport、可信 client session／authentication、主執行緒 pump、關閉與重連規則。

上述工作目前不執行；既有程式與測試保留。未來需補真正跨程序的端到端測試，現在只有 JSON boundary 的 in-process 實作，不宣稱已完成遠端連線。
