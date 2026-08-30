# 07 — Diagnostics：oracle、trace 與首次失敗

[上一章：Observation／hash](06-observation.md) · [教材索引](README.md) · [下一章：Recording／Replay](08-replay.md)

本章問題：遊戲沒有 exception，是否就代表正確？當 pipeline 已完成但 registry/repository 不一致，或一個測試邊界被突破，需要可重現的失敗證據，而不是讓 Overlay 每次刷新時偷偷再跑檢查。

## 先分開兩種 invariant

Domain invariant 在操作當下維持規則，例如 Health 不負數、死亡清方向、Position 必須有限。它們位於 Actor／Position，不依賴 testability。

post-tick invariant／oracle 讀取 observation，檢查整合結果或測試政策。例如：commit 後不應還有死者、registry 活動數應等於 repository snapshot 數。它不能取代 Domain 的防線，也不能修改世界來「修好」違規。

[ArenaEvidence.cs](../../Assets/game/arena/src/Integration/ArenaEvidence.cs) 提供兩個實作：

- `ArenaInvariant`：正式的 committed-state 檢查，驗證身分順序、血量、方向與 registry/repository 數量。
- `TrainingPositionOracle`：只在示範故障時啟用，玩家 X > 1.5 回 `tutorial.position-limit`。它不是 Arena 的移動限制，也不在正常 Unity composition 啟用。

## 接點一：每個 session 都建立新的 checks

這段位於 ArenaDefinition：

```csharp
protected override void ConfigureInvariants(
    InvariantRegistry<ArenaObservation> invariants)
{
    invariants.Register(new ArenaInvariant());
    if (failureOracle)
        invariants.Register(new TrainingPositionOracle());
}
```

framework 建立 registry、呼叫 ConfigureInvariants、Seal，並在每個 tick 的 capture/hash 後 Evaluate。Definition 只保存 bool 組裝選項，不共用會累積狀態的 invariant instance。

tick 0 有 observation/hash，但 invariant report 尚未 Evaluated。consumer 應同時看 Evaluated、report.Tick 和 failure，而不是只顯示一個綠色 PASS。

啟用 oracle 時，ArenaDefinition.PolicyId 加上 `/training-position-oracle-v1`。normal policy 不得把少了一個 oracle 的 replay 誤判為已重現同樣失敗。

## 接點二：讓 trace 知道遊戲訊息的原因

framework 自動記錄 admission、phase、dispatch、ActionResult、hash 與 failure；它不知道 ArenaFact.Actor 的語意。ArenaDefinition 因此提供 DescribeInput／DescribeMessage，委派 [ArenaSimulationWiring.Describe](../../Assets/game/arena/src/Integration/ArenaSimulationWiring.cs)。

例如對 fact message 回傳：

```csharp
return new TemplateTraceMetadata(
    fact.Fact.Kind.ToString(),
    fact.Sequence,
    fact.Fact.Actor.Value,
    fact.Fact.Target.Value,
    fact.Fact.Amount.ToString(CultureInfo.InvariantCulture));
```

這是 Describe 的 ArenaFactMessage 分支，引用 `Testability.Templates`、`System.Globalization`。它只描述資料，不改 dispatch；額外 event 的 causation 需由第 5 章明確攜帶 sequence，不能指望框架猜出是哪個外部要求。

Action sequence 和 trace record sequence 不相同。前者連回操作；後者只是診斷分頁 cursor。phase／自主 commit 通知可能沒有單一外部原因，不應硬塞最後一筆 action sequence。

## 接點三：只把 reader 交給診斷 consumer

以下放在已有 `session` 的測試／console 方法，引用 `Testability`、`TraceBuffering`、`Arena.Integration`：

```csharp
IDiagnosticReader<ArenaObservation> reader = session.Diagnostics;
DiagnosticSnapshot<ArenaObservation> snapshot = reader.ObserveDiagnostics();
TraceCursor cursor = default;
TraceBatch<TraceEntry> batch = reader.ReadTrace(cursor, 64);
cursor = batch.NextCursor;
```

讀取不 Step、不重新 capture、不重算 invariant，不新增 trace。多次 Poll 不應改變 gameplay hash。Trace 有界；讀者落後會看見 MissedCount／StreamChanged，不能把缺失資料說成沒有事件。

Unity 的 [ArenaDiagnosticsPanel](../../Assets/game/arena/src/Unity/ArenaDiagnosticsPanel.cs) 只取得這個 reader。來源 overwrite、尚未讀到就遺失、面板本地歷史淘汰是三件不同事；reader 無法 Submit／Step／Reset。

它現在是「診斷 presenter」，不是一個自行繪圖的 `OnGUI` 面板：

- `Refresh` 在可見時以最多約 10 Hz 輪詢；session／stream 改變時清除舊歷史與缺口計數。
- 每批最多讀 512 筆，面板只保留最新 160 筆。超過本地上限的資料算 `LocalEvictedCount`，不要和來源 `OverwrittenCount`、游標 `MissedCount` 混成一個數字。
- 新保留的 record 才建立 `ArenaTraceRow`，預先格式化兩行 `Summary` 與完整 `Detail`。已經知道會淘汰的舊資料不再格式化。
- `TraceRows` 是同一個唯讀清單介面。`TraceRevision` 表示列資料改變；`Revision` 表示標題、snapshot 文字或錯誤文字改變。
- Hide evidence 只停止自動讀取／格式化，不停止 simulation。重新顯示立即續讀原 cursor，若來源期間已覆寫，必須顯示缺口。測試或明確 Step 使用的 `Poll()` 刻意不受顯示狀態和節流限制。

[第 10 章](10-unity.md) 才把這個 presenter 接給 UI Toolkit view。虛擬化只限制建立幾個畫面元素；有界歷史、少做格式化及只讀 capability 仍是 presenter 自己的責任。

## 在沒有 exception 的情況下產生首次 failure

以下片段放在測試／console 方法中：

```csharp
using System;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;
using Testability;
using Testability.Templates;

ArenaDefinition definition = new ArenaDefinition(failureOracle: true);
using (TestableSimulationSession<ArenaRuntime, ArenaScenario,
    ArenaInput, ArenaObservation> session = definition.CreateTestSession(
    new ArenaScenario(tickDelta: .25f)))
{
    session.Gameplay.Submit(session.Id, 1, 1,
        new ArenaInput(ArenaAction.Move, session.Observe().PlayerId, x: 1f));
    session.Simulation.Step(); // X=1，仍合法
    session.Simulation.Step(); // X=2，oracle 失敗

    Console.WriteLine(session.State == SessionState.Faulted); // True
    Console.WriteLine(session.Failure.Code); // tutorial.position-limit
    Console.WriteLine(session.Failure.Tick); // 2
    Console.WriteLine(session.LastCompletedTick); // 1
    TemplateRecording evidence = session.CaptureRecording();
}
```

X=2 是合法 Domain 狀態，只違反測試 oracle。不要修改 Actor 加入 `X <= 1.5` 來消除這個教材失敗；那會把測試政策錯當遊戲規則。

## failure 之後能做什麼

- 保存第一次 failure 的 stage、attempted tick、LastCompletedTick、sequence、code、exception type 等證據。
- 讀 Diagnostics，並區分 ObservationTick；不承諾取得任意 partial-world state。
- CaptureRecording，交給下一章的 Replay。
- 不再 Step；Stop 不覆蓋第一次故障。要繼續新實驗，Reset 或建立新 session。

已執行且 Accepted 的操作不回滾。同 tick 未完成與尚未到期的輸入也不能捏造成功結果；外部 future inputs 由結果查詢顯示取消。limits 只限制可返回的 tick／資料容量，不會中止永不返回的 callback；沒有 process watchdog。

## 執行、預期與反例

```powershell
dotnet run --project tools/arena-checks -- diagnostics
```

此 selector 檢查 reader 的唯讀性、Reset stream identity、來源 overwrite、非 crash oracle failure，以及 phase exception 保留上一份 observation 並可重播。action／fact／command 的 trace causation 由第 5 章 lifecycle selector 驗證；Unity 面板自己的 history／gap 行為另由 PlayMode 測試驗證。具體本次通過情況由執行結果決定。

反例：在 Overlay.Refresh 直接呼叫 invariant.Evaluate 或 Application.Execute，會讓顯示次數影響診斷／世界；應移回 framework 的固定評估流程。另一個反例是只看最近 report 是 PASS 而忽略 report tick，導致把上一 tick 成功當成故障 tick 成功。

下一章把同樣的 failure 以 JSON 保存，並要求在乾淨世界再次出現同樣的 fingerprint，而不只是播放動畫到同一畫面。
