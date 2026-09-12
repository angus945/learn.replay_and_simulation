# 04 — Input：接正式控制面與操作結果

[上一章：Simulation](03-simulation.md) · [教材索引](README.md) · [下一章：Lifecycle／RNG](05-lifecycle.md)

本章問題：外部工具要求「tick 2 向右」時，怎麼區分收到了要求、執行了要求，以及遊戲拒絕要求？直接呼叫 Application.Execute 無法表達這些不同時機。

`ArenaControlAdapter` 使用 `module.runtime-control` 提供 operation admission／state，`ArenaSession` 再把到期輸入送入第 3 章的同一 pipeline。Application 仍只處理遊戲是否合法。

## 新增 payload 與 mapping，別再造一個 input dispatcher

[ArenaInput](../../Assets/game/arena/src/Integration/ArenaInput.cs) 是可序列化外部 payload，只包含 Kind、Actor、Target、X、Y。它沒有 SessionId、Sequence、TargetTick，也不實作 IIntent。

這不是少接一層。Arena 明確提供：

```text
ArenaSession.Submit
  → ArenaControlAdapter 保存 payload 與 OperationHandle
  → 到期時轉成 ArenaInputIntent
  → ArenaInputCommand
  → ArenaSimulationWiring.Execute
  → ArenaApplication.Execute
```

Arena 不另外註冊一套 Move intent handler，不讓 Unity 或測試走第二條入口。

在 [ArenaSimulationWiring](../../Assets/game/arena/src/Integration/ArenaSimulationWiring.cs) 的正式入口是：

```csharp
public static ArenaInputOutcome Execute(ArenaRuntime runtime, ArenaInput input, ArenaInputExecutionContext context)
{
    // map to ArenaRequest, then call ArenaApplication.Execute
}
```

這個 context 帶外部 sequence／target tick 及 event sink，只停留在外圍。Integration 將數字 ID 轉 ActorId、payload 轉 ArenaRequest，再把 ArenaDecision 轉 ActionStatus。Domain/Application 不需要 `InputExecutionContext`。

Input bridge 是 Arena Integration 的明確組裝內容，不由通用 module 猜測或自動註冊，避免把「類別能編譯」誤當成已接完正式輸入。

## 實作兩階段驗證

Admission 發生在 Submit：

- Operation identity 與 sequence 由 session-owned registry 產生，caller 不可自訂。
- TargetTick 必須大於 CurrentTick 且在 tick 預算內。
- codec 與單筆／總 payload／輸入數量預算必須通過。
- 成功只保存獨立的編碼 payload，回 `Admitted`；此刻不扣血、不移動。

Execution 發生在到期 tick：

- 同 tick 按 registry 產生的 Sequence 排序。
- decode 獨立 input，再由 Application 檢查 actor／target／距離。
- 成功或業務拒絕都產生 `ArenaOperationResult`；未知 actor 是 `Rejected / actor-not-found`，不是 session fault。

「輸入有合法 envelope」不能提前證明目標仍活著；排隊到執行之間，前一筆操作可能已殺死目標。

## 用一組輸入看見順序

以下片段放在測試／console 方法中：

```csharp
using System;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;
using RuntimeControl;

using (ArenaSession session = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f)))
{
    ulong player = session.Observe().PlayerId;
    OperationAdmission first = session.Submit(new ArenaInput(ArenaAction.Move, player), 2);
    OperationAdmission second = session.Submit(new ArenaInput(ArenaAction.Move, player, x: 1f), 2);
    session.Submit(new ArenaInput(ArenaAction.Move, 999, x: 1f), 2);

    Console.WriteLine(first.IsAdmitted && second.IsAdmitted); // True
    ArenaTickEvidence one = session.Step();                    // 沒有到期輸入
    ArenaTickEvidence two = session.Step();
    Console.WriteLine(one.Results.Count);            // 0
    Console.WriteLine(two.Results[0].Sequence);      // 1
    Console.WriteLine(two.Results[1].Sequence);      // 2
    Console.WriteLine(two.Results[2].Code);          // actor-not-found
    Console.WriteLine(session.Observe().FindActor(player).X); // 1
}
```

最後方向為 sequence 2 的右移，之後才進入 PrePhysics，所以玩家在該 tick 移動。Sequence 是 admission 次序；`moved` 表示「設定方向這個用例已接受」，不是 Submit 時已移動，也不是整個 tick 一定成功。

## 把需要的 port 分發給正確 consumer

- gameplay host 使用 `ArenaSession.Submit`／`Observe`，不給任意 world setter。
- `ArenaControlAdapter` 提供 `Find`／`Read`，不從 trace 猜測成功。
- Overlay 只取得 `IArenaDiagnosticReader`，第 7 章說明。
- Composition 保有完整 session，負責 Step／Stop、建立 driver、CaptureRecording 及 Dispose。

不要把全 session 塞給每一個 consumer，再要求它們「自律不呼叫其他方法」。ports 是工程上的最小能力邊界，不是對惡意同程序 C# 程式的安全防護。

`Controls.Find(handle)` 可區分 Pending、Running、Succeeded、Rejected、Failed、Cancelled，以及 Found／Unknown／Evicted／ForeignHandle。分頁 Read 的 afterIndex 是已讀完成結果數，不是 operation sequence。

## 新 session、Stop 與 limits 也是正式契約

重新開始時建立新的 ArenaSession，因此有全新 world／seed／identity；舊 `OperationHandle` 對新 registry 會是 ForeignHandle。這避免 Reset 在準備或 cleanup 半途失敗時混用兩代狀態。

ArenaScenario 的預算由 `CreateLimits` 映射。明確傳入 `ArenaLimits` 代表 host 有意覆寫；實際 limits 會保存到 recording。輸入數量是整段 session 的累計，不是只算目前 queue，因此執行完不會「退還」input 額度。

Stop／Dispose／Fault 將未執行輸入完成為 Cancelled，不偽造沒有執行過的成功結果。已完成的結果保留，即使該 tick 後續 oracle 失敗。

## 執行、預期與反例

```powershell
dotnet run --project tools/arena-checks -- input
```

預期：入列不改狀態、同 tick 按 sequence、duplicate／stale identity／預算受控，業務拒絕不 fault。不要只驗證成功 Move。

反例：重複提交 sequence 1 應在 admission 拒絕；改成 sequence 4、actor 99 則可排入，直到 execution 才回 actor-not-found。這兩個失敗不能被 UI 合併成相同意思。

下一章讓致死 Attack 產生的 ArenaFact 經由內部 reaction 走到 StructuralCommit。它不是第四種外部輸入，不該再被 Submit 或錄製一次。
