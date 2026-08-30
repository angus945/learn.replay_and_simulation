# 04 — Input：接正式控制面與操作結果

[上一章：Simulation](03-simulation.md) · [教材索引](README.md) · [下一章：Lifecycle／RNG](05-lifecycle.md)

本章問題：外部工具要求「tick 2 向右」時，怎麼區分收到了要求、執行了要求，以及遊戲拒絕要求？直接呼叫 Application.Execute 無法表達這些不同時機。

Testability 的 gameplay port 提供 envelope admission，模板再把到期輸入送入第 3 章的同一 pipeline。Application 仍只處理遊戲是否合法。

## 新增 payload 與 mapping，別再造一個 input dispatcher

[ArenaInput](../../Assets/game/arena/src/Integration/ArenaInput.cs) 是可序列化外部 payload，只包含 Kind、Actor、Target、X、Y。它沒有 SessionId、Sequence、TargetTick，也不實作 IIntent。

這不是少接一層。`ReplayableSimulationDefinition` 已自動提供：

```text
Gameplay.Submit 的 envelope
  → 到期時轉成框架 InputIntent
  → 框架 InputCommand
  → ArenaDefinition.ExecuteInput
  → ArenaSimulationWiring.Execute
  → ArenaApplication.Execute
```

Arena 不另外註冊一套 Move intent handler，不讓 Unity 或測試走第二條入口。

在 [ArenaDefinition](../../Assets/game/arena/src/Composition/ArenaDefinition.cs) 補上的 hook 是：

```csharp
protected override InputOutcome ExecuteInput(ArenaRuntime world,
    ArenaInput input, InputExecutionContext context)
    => ArenaSimulationWiring.Execute(world, input, context);
```

這個 context 帶外部 sequence／target tick 及 event sink，只停留在外圍。Integration 將數字 ID 轉 ActorId、payload 轉 ArenaRequest，再把 ArenaDecision 轉 ActionStatus。Domain/Application 不需要 `InputExecutionContext`。

模板的 ExecuteInput 是 virtual 而不是 abstract；完全沒覆寫時會在執行拋錯。Arena 明確覆寫 context overload，避免把「類別能編譯」誤當成已接完正式輸入。

## 實作兩階段驗證

Admission 發生在 Submit：

- SessionId 必須是這個 session 的目前 identity。
- Sequence 必須非零、這個 session 尚未使用。
- TargetTick 必須大於 CurrentTick 且在 tick 預算內。
- codec 與單筆／總 payload／輸入數量預算必須通過。
- 成功只保存獨立的編碼 payload，回 Queued；此刻不扣血、不移動。

Execution 發生在到期 tick：

- 同 tick 按 Sequence 排序，不按提交先後。
- decode 獨立 input，再由 Application 檢查 actor／target／距離。
- 成功或業務拒絕都產生 ActionResult；未知 actor 是 `Rejected / actor-not-found`，不是 session fault。

「輸入有合法 envelope」不能提前證明目標仍活著；排隊到執行之間，前一筆操作可能已殺死目標。

## 用一組輸入看見順序

以下片段放在測試／console 方法中：

```csharp
using System;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;
using Testability;
using Testability.Templates;

using (TestableSimulationSession<ArenaRuntime, ArenaScenario,
    ArenaInput, ArenaObservation> session = new ArenaDefinition()
    .CreateTestSession(new ArenaScenario(tickDelta: .25f)))
{
    ulong player = session.Gameplay.Observe().PlayerId;
    SubmissionResult second = session.Gameplay.Submit(session.Id, 2, 2,
        new ArenaInput(ArenaAction.Move, player));
    SubmissionResult first = session.Gameplay.Submit(session.Id, 1, 2,
        new ArenaInput(ArenaAction.Move, player, x: 1f));
    session.Gameplay.Submit(session.Id, 3, 2,
        new ArenaInput(ArenaAction.Move, 999, x: 1f));

    Console.WriteLine(first.Queued && second.Queued); // True
    TemplateTick one = session.Simulation.Step();     // 沒有到期輸入
    TemplateTick two = session.Simulation.Step();
    Console.WriteLine(one.Results.Count);            // 0
    Console.WriteLine(two.Results[0].Sequence);      // 1
    Console.WriteLine(two.Results[1].Sequence);      // 2
    Console.WriteLine(two.Results[2].Code);          // actor-not-found
    Console.WriteLine(session.Observe().FindActor(player).X); // 0
}
```

最後方向為 sequence 2 的停止，之後才進入 PrePhysics，所以玩家留在原點；先提交 sequence 2 不代表它先執行。`moved` 表示「設定方向這個用例已接受」，不是 Submit 時已移動，也不是整個 tick 一定成功。

## 把需要的 port 分發給正確 consumer

- gameplay caller 取得 `ITemplateGameplay<ArenaInput, ArenaObservation>`：Submit／Observe，不給任意 setter。
- 手動測試 runner 取得 `ITemplateSimulation`：Step。
- 測試條件管理者取得 `ITemplateAdmin<ArenaScenario>`：Reset／Stop。
- 結果 reader 取得 `ITemplateResults`：Find／Read，不從 trace 猜測成功。
- Overlay 只取得 `IDiagnosticReader<ArenaObservation>`，第 7 章說明。
- Composition 保有完整 session，負責建立 driver、CaptureRecording 及 Dispose。

不要把全 session 塞給每一個 consumer，再要求它們「自律不呼叫其他方法」。ports 是工程上的最小能力邊界，不是對惡意同程序 C# 程式的安全防護。

`Results.Find(sessionId, sequence)` 可區分 Pending、Completed、Cancelled、Unknown、StaleSession。分頁 Read 的 afterIndex 是已讀完成結果數，不是 action sequence；搭配 session ID 保存，不能混入新 session。

## Reset、Stop 與 limits 也是正式契約

Reset 以 scenario 建立全新 world／seed／identity，舊 identity 不可繼續提交。Testability 先準備候選世界、checks 與初始 hash；準備失敗保留原 session。cleanup 失敗不承諾恢復，所以 world 不應用排他式全域 singleton。

ArenaScenario 的預算由 `CreateDefaultLimits` 映射。明確傳入 TemplateLimits 代表 host 有意覆寫；實際 limits 會保存到 recording。輸入數量是整段 session 的累計，不是只算目前 queue，因此執行完不會「退還」input 額度。

Stop/Fault 取消未執行輸入，Find 回 Cancelled，不偽造沒有執行過的 ActionResult。已完成的結果保留，即使該 tick 後續 invariant 失敗。

## 執行、預期與反例

```powershell
dotnet run --project tools/arena-checks -- input
```

預期：入列不改狀態、同 tick 按 sequence、duplicate／stale identity／預算受控，業務拒絕不 fault。不要只驗證成功 Move。

反例：重複提交 sequence 1 應在 admission 拒絕；改成 sequence 4、actor 99 則可排入，直到 execution 才回 actor-not-found。這兩個失敗不能被 UI 合併成相同意思。

下一章讓致死 Attack 產生的 ArenaFact 經由內部 reaction 走到 StructuralCommit。它不是第四種外部輸入，不該再被 Submit 或錄製一次。
