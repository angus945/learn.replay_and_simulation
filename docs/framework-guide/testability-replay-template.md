# Testability／Replay：可直接繼承的模板

[回到索引](README.md) · [基本 Simulation 模板](definition-template.md)

本模板位於 `Framework.Testability`，namespace `Testability.Templates`，實際類別為
`ReplayableSimulationDefinition<TWorld, TScenario, TInput, TObservation>`。
它繼承基本 SimulationDefinition，但由框架補上外部 Input Intent → Internal Command → ExecuteInput 的橋接。
Domain 不需要實作框架介面；你保留自己的 DDD 寫法。

## 必填接點：編譯器會指出尚未完成的責任

| 接點 | 你實作的內容 |
|---|---|
| ValidateScenario／GetTickDelta | 場景規則與固定 tick 秒數 |
| CreateWorld／DestroyWorld | 用自己的方式建立全新 Domain/services，以及釋放資源 |
| ConfigureWorld | 註冊專案 phase participants、額外 commands／domain event handlers |
| ExecuteInput | 執行正式玩法，回傳 InputOutcome；需要時透過 events 發布事實 |
| CaptureObservation | 回傳不可變 snapshot，包含狀態比較需要的資料 |
| EncodeCanonicalState | snapshot → 穩定 bytes；框架計算 SHA-256 |
| ConfigureInvariants | 註冊每個 session 全新的規則，框架負責 Seal／Evaluate；沒有規則也需明確實作 |
| EncodeScenario／DecodeScenario | scenario 的穩定序列化與獨立重建 |
| EncodeInput／DecodeInput | 外部輸入的穩定序列化與獨立重建 |
| PolicyId | 規則／codec／hash／invariant 版本識別；規則改變時由專案明確更新 |

不再 override 基本 Configure，它在此模板中 sealed；請 override ConfigureWorld。
不要把可變 world／RNG／invariant instance 放在共用 definition 欄位中。每個 world 必須獨立。
Codec 必須純粹、可重複，decode 回傳新物件／不可變值；不可依呼叫次數改變結果。
Payload 是字串，內部格式由你決定；外層 recording 使用固定 DataContract JSON，不儲存任意 CLR type name。

## 可執行參考

[TemplateContractChecks.cs](../../Assets/framework.testability/tests/TemplateContractChecks.cs)包含完整 ReplayCounterDefinition：

- TemplateCounter 是普通 domain class，不繼承 framework。
- CounterInput 刻意可變，測試證明 Submit 後改原物件不影響已錄製輸入。
- CounterSnapshot 是不可變 class。
- ExecuteInput 接受設定值，負值回業務拒絕；測試用 999／旗標注入錯誤。
- Policy、canonical bytes 與 invariant 都由此 definition 明確提供。

這是 framework 的參考測試，不是 production 要引用的測試 assembly。新專案把自己的 definition 放在 game/<game>-simulation/src。
測試中的 BrokenObservation／BrokenHash／BrokenPhase、DuringExecute 等是故障注入，正式專案不需複製。

以下是該範例的使用方式（實際可執行對應測試為 ReplayFrameMatrix）：

```csharp
ReplayCounterDefinition definition = new ReplayCounterDefinition();
TemplateRecording recording;
using (TestableSimulationSession<TemplateCounter, int, CounterInput, CounterSnapshot> session =
    definition.CreateTestSession(0))
{
    SubmissionResult admission = session.Gameplay.Submit(
        session.Id, 1, 1, new CounterInput { Amount = 5 });
    TemplateTick tick = session.Simulation.Step();
    CounterSnapshot snapshot = session.Gameplay.Observe();
    recording = session.CaptureRecording();
}
using (TemplateReplay<TemplateCounter, int, CounterInput, CounterSnapshot> replay = definition.CreateReplay(recording))
{
    replay.Step(); // 初始 Paused；這筆 recording 只有一個 tick。
    // State == Completed、FirstDifference == null。
}
```

請用 CreateTestSession，繼承而來的 CreateSession 是低階 simulation host，不包含記錄／測試控制面。
需要的 asmdef 引用：Framework.Testability、Framework.DeterministicSimulation、Module.SimulationPrimitives、Module.InvariantChecks，以及自己的 domain assemblies。

## 框架提供的正式控制面

| Port／操作 | 用途 |
|---|---|
| Gameplay.Submit／Observe | 送外部輸入、讀取已捕捉 snapshot |
| Simulation.Step | 推進一個 tick，取得 TemplateTick（hash、ActionResults） |
| Admin.Reset／Stop | 建立新 session identity、停止執行 |
| Results.Find | StaleSession／Unknown／Pending／Completed／Cancelled |
| Diagnostics | IDiagnosticReader：唯讀 snapshot 與 cursor trace，不能 cast 回 Step／Admin |
| CaptureRecording | 取得正常或首次失敗的可序列化錄製 |

這版採 Manual 驅動，單一 owner thread；沒有內建 Realtime clock claim 或 player input adapter。
可由專案自己的 frame adapter 在 owner thread 呼叫 Step，但不可同時再給另一個 driver 推進。
Port 是整合邊界，不是防止惡意同程序程式的安全沙箱。

## Admission、執行與診斷時機

Submit 驗證 session ID、sequence、target tick、容量；成功時只保存編碼字串，不保存呼叫者的可變 input。
每個 tick 的輸入按 sequence 排序，在執行前 decode，進入三類訊息橋接。
ExecuteInput 是 Internal Command handler 的專案接點；一般業務拒絕回 Rejected，非法玩法參數可回 InvalidRequest。
Exception 代表 simulation fault，不代表可重試的普通拒絕。

成功 tick：Pipeline → CaptureObservation → hash → invariants → LastCompletedTick。
讀取 Gameplay.Observe／Diagnostics 只回快取，不重新 capture／hash／Evaluate，也不新增 trace。
初始 tick 0 建立 snapshot/hash，invariant report 是 Not Evaluated；第一次 Step 才評估 checks。
Phase 與 dispatch trace 有界；ActionResult trace 帶 action sequence。額外 domain event 的 trace 不自動繼承 action correlation。

## 首次失敗證據與重現

TemplateFailure 保存 attempted Tick、LastCompletedTick、Sequence、Stage、Code、ExceptionType、Detail。
同 tick 尚未完成的輸入記為 Failed；觸發例外的 input 是 simulation.exception，其他為 tick.aborted。
Invariant 失敗可能已經有 hash 和 Accepted action；不把它們回滾或改寫成尚未執行。
未來外部輸入回 Cancelled／session.faulted，Stop 則為 session.stopped；它們沒有偽造的 ActionResult。
失敗後 Step 禁止，Stop 不覆寫第一次證據，Reset 才建立新世界。

如果 observation capture 沒成功，診斷保留上次成功的 immutable snapshot；請讀 DiagnosticSnapshot.ObservationTick，不能把它當成失敗瞬間的完整狀態。
錄製保存輸入、逐 tick 結果／hash、首次失敗與有限 trace；不序列化任意 TObservation，亦不是部分世界的 restore checkpoint。
目前不自動比較 exception stack/detail 文字，重現比較 tick／code／stage／sequence／last-completed／exception type 與全部結果／hash。

## Replay 的行為

CreateReplay 驗證 recording schema、範圍、tick 序列、input/result 對應與容量，再重建獨立 session。
提供 Play／Pause／Step／AdvanceTime／Restart；沒有 Submit，因此播放端不能加入即時輸入。
每 rendered frame 最多推進 120 ticks，保留 backlog，不偷偷丟 simulation time。

- Completed：正常錄製全部相符。
- ReproducedFailure：在預期 tick 重現同一個 failure，這是重現成功，不是正常玩法成功。
- Diverged：第一個 policy／tick delta／initial hash／tick hash／action result／failure 差異，保存 FirstDifference。
- Runtime 不同只給 warning，不能據此保證跨平台 determinism。

Restart 重建 session。先前拿到的 Replay.Diagnostics 是舊 session 的 facade，Restart 後請重新取得。
CreateReplay／Restart 遇到非法 codec 或組裝錯誤會拋出，不當成成功播放；此情況需處理錯誤並釋放 replay。

## JSON 與容量

使用 TemplateRecordingIO.Write(stream, recording)／Read(stream)，呼叫者擁有 stream 與路徑，不會被工具關閉，也不會自動覆寫檔案。
Reader 在反序列化前限制整份資料大小，預設 16 MiB，可設定到 64 MiB；超過明確拒絕。
TemplateLimits 預設：10,000 ticks、10,000 inputs、512 traces、單筆 payload 64 KiB、scenario＋inputs payload 合計 4 MiB。
這不是精準 heap 上限，JSON escaping／結果／trace 仍有額外大小。寫入無自動截斷；大型合法錄製可能需調高 Read 的 file byte limit。
錄製包含尚未到期輸入，但只重播到錄製最後一個 tick。Reset 會清掉舊歷史並更換 identity；跨 Reset 請分開保存錄製。

## Reset 與清理保證

Testability host 先建立候選世界、checks、初始 observation/hash；這些步驟失敗時保留原 session。
準備成功才 Dispose 舊世界並換新 identity。候選世界與舊世界會短暫並存，因此 world 不可依賴全域單例或排他式共享資源。
舊世界 cleanup 失敗時，新世界也清理，host 變成 disposed，需建立新的 host；不承諾 rollback cleanup。
這比基本 SimulationSession 的 destroy-then-create Reset 更保守；兩者不是同一個 Reset 實作。

## 專案仍需負責

- 所有會影響未來的權威資料都要納入 snapshot／canonical bytes，包含 RNG state、待執行排程、必要的身分配置狀態。
- 用 PolicyId 明確識別規則、codec、hash schema、invariant 版本；框架無法推斷程式變更，必要時包含 build revision。
- Snapshot 真正不可變、codec 沒有副作用、callback 有界；沒有 process watchdog 或 deep-freeze 任意物件。
- 不在 ConfigureWorld 註冊另一個未記錄的外部 input source，否則錄製不完整。
- Domain event／Internal Command 是重新執行的結果，不作為錄製輸入。

MovementDemo 現在透過 GameplayDefinition／GameplayWorld 使用新模板；舊 GameplaySession 保留給既有 Protocol 與舊格式測試。沒有擴充 Protocol、snapshot seek/restore、rollback 或跨平台 bitwise 保證。
新 TemplateRecording schema 與既有 Gameplay ReplayArtifact 是不同格式，不互相冒充相容。

## 本輪驗證（2026-08-30）

- Unity 編譯無錯誤，EditMode 158/158 通過。
- 純 .NET gameplay-checks 通過，含基本模板 5 組與 testability／Replay 模板 8 組契約檢查。
- 正常重播覆蓋 30/60/144 FPS 與不規則 frame delta；例外、invariant、phase、observation/hash 失敗均驗證重現。
- 驗證 JSON round trip、錄製檔案大小上限、輸入容量、readonly facade、Reset stream identity 與 owner-thread/reentry 限制。
- 初次模板驗證未修改 Demo；後續 Demo 改接與驗證見 [Demo 整合](demo-template.md)。
