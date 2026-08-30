# 09 — Realtime：只有一個時鐘擁有者

[上一章：Recording／Replay](08-replay.md) · [教材索引](README.md) · [下一章：Unity host](10-unity.md)

本章問題：Unity 一個 frame 可能不到一個 tick，也可能要補跑多個 tick。誰累積時間、何時消耗按鍵、何時保存前後 snapshot？若 host 自己 Step 又建立 runner，就會重複推進世界。

framework 的 RealtimeSimulationRunner 負責時間與唯一驅動權，ArenaLiveSession 只負責 input／presentation adapters。

## 新增哪個組裝物件

[ArenaLiveSession](../../Assets/game/arena/src/Composition/ArenaLiveSession.cs) 是純 C#，實作 `IRealtimeInputSource`、`IRealtimePresentation`、`IDisposable`。它私有持有：

- 一個 TickInputBuffer：保存最近的軸值與按下邊緣。
- 由普通 ArenaDefinition 建立的 TestableSimulationSession。
- 從該 session.CreateRealtimeRunner 取得的唯一 runner。
- 這段 session 的 input sequence，以及最近兩個 tick 的 immutable observation。

它沒有第二份 Actor、沒有自己的 accumulator、沒有另一套 attack 規則。Unity host 也拿不到完整 session 來額外 Step。

## 接點一：只向 session 取得 runner

ArenaLiveSession constructor 的核心接線：

```csharp
session = new ArenaDefinition().CreateTestSession(
    scenario ?? new ArenaScenario());
PreviousObservation = session.Observe();
CurrentObservation = PreviousObservation;
runner = session.CreateRealtimeRunner(input: this, presentation: this);
```

框架私有 tick source 保證 runner 仍走 Testability.StepCore，因此 results、hash、invariant、trace、recording 沒有被即時模式繞過。

持有 runner 時，公開 manual Step、Reset、session.Dispose 受 ownership 限制。Pause 只清除累積時間並保留所有權，不是把 manual drive 權交出去。真正要手動接管，需在 callback 外先 Dispose runner，再操作 session；ArenaLiveSession 封裝內沒有提供這條控制路徑。

## 接點二：frame 取樣與 tick 消耗分開

Unity 每個 frame 呼叫 `CaptureAxes`／`CaptureAttack`；它們只更新 buffer，不直接改 Actor。runner 在每個即將執行的 tick 呼叫 AcquireInput：

1. `input.ConsumeTick(tick.Number)`。
2. 將最新 X/Y 送為 Move input，指定該 tick，sequence 遞增。
3. 若 Attack 有新的 Pressed edge，從 snapshot 挑選最近敵人，送一筆 Attack。
4. 全部經 `session.Gameplay.Submit`，沒有直接呼叫 Application。

選最近敵人是輸入 adapter 的目標選擇；AttackRange 驗證仍在 Application。因此 adapter 選到距離外目標只得到正常 out-of-range，不把這條規則再實作一份。

同一 frame 補跑多 ticks 時沿用軸值，Attack edge 只消耗一次。沒有 timestamp 的短暫鍵盤變化可能落在兩次 frame 取樣之間而遺失；此 adapter 不宣稱不同真實 FPS 會產生完全相同的 input/tick 分配。

ClearInput 在 buffer 曾被取樣後重新建立 host-owned TickInputBuffer，清除舊 axes／edges，不改 simulation tick 或 recording。`inputDirty` 讓已清空的 buffer 不因持續失焦／暫停而每 frame 重建。清完後新取樣的 press 仍要被接受；不要用「無條件忽略下一次 Attack」的旗標，把切換模式後的新按鍵一起吞掉。

`CaptureAxes`、`CaptureAttack`、`ClearInput` 也受 owner-thread 與 disposed guard 保護，不能因為它們「只改 buffer」就讓背景執行緒搶寫。非建立執行緒呼叫會拋 InvalidOperationException；在 owner thread 對已 Dispose 的 live session 呼叫會拋 ObjectDisposedException。背景來源應先排入 host 的 owner thread，再取樣，不直接把 transport callback 接到這些方法。

## 接點三：逐 tick 保存 snapshot pair

ArenaLiveSession 的 IRealtimePresentation hook：

```csharp
void IRealtimePresentation.CaptureTickState(ulong tick)
{
    PreviousObservation = CurrentObservation;
    CurrentObservation = session.Observe();
}
```

捕捉發生在每一個 tick 後，不是 frame 結束只捕捉一次。因此一次補跑到 tick 5，呈現的 pair 是 tick 4／5，而不是上次畫面的 tick 1／5。

此純 C# adapter 的 Render 不操作 Unity；Unity host 在 LateUpdate 讀 pair 和 PresentationAlpha 交給 view adapter。若 simulation fault 發生在 capture 前，observation 可能是最近成功 tick；consumer 應按實際 snapshot tick 判斷跳躍／snap。

一般 Running 狀態由 runner 提供 interpolation alpha；Pause、Stopped 或 Faulted 時，ArenaLiveSession.PresentationAlpha 明確回 1，顯示目前可用的最新 observation，而不是停在上一段插值的起點。這也涵蓋 tick budget 結束與明確 Stop。Faulted 的「最新」仍指已捕捉 snapshot，不代表復原失敗中途的 world；例如 tick 2 在 capture 前失敗，可能顯示 tick 1 的有效狀態。

這是 Arena 的呈現政策，沒有修改 framework 的權威 tick 或 hash，也沒有把故障世界當成完成了該 tick。

## 無 Unity 也能驗證即時接線

以下片段放在測試／console 方法中：

```csharp
using System;
using Arena.Composition;
using Arena.Integration;

using (ArenaLiveSession live = new ArenaLiveSession(
    new ArenaScenario(tickDelta: .25f)))
{
    live.CaptureAxes(1f, 0f);
    live.AdvanceTime(.5f); // 同一次 frame 補跑兩個 tick
    live.UpdatePresentation();

    ArenaObservation current = live.Observe();
    Console.WriteLine(live.TickNumber); // 2
    Console.WriteLine(live.PreviousObservation.Tick); // 1
    Console.WriteLine(current.FindActor(current.PlayerId).X); // 2
    Console.WriteLine(live.CaptureRecording().Inputs.Count); // 2 個 Move
}
```

此結果仍可交給第 8 章的 TemplateReplay。回放不需模仿原來的 `.5f` frame，只需重送錄製中已分配到 tick 的輸入。

## 時間、停止與 ownership

- `AdvanceTime` 接收有限非負秒數，每次最多處理設定的 tick 數；預設上限 120。剩餘 debt 保留，沒有偷偷丟 tick。
- `Pause` 清除 accumulator，Resume 不補跑暫停期間；Arena 同時清掉待消耗的攻擊邊緣。
- 持續輸入、pause／return-live／失焦時要明確清 buffer，避免模式切換後多打一發。
- input/presentation adapter exception 記在 runner.Failure，不一定是 domain session fault。不要每 frame 重試失敗的 driver。
- 正式 session fault 或 budget 結束會停止追趕，保留其 recording／diagnostics。
- 所有操作在 owner thread，不是背景 thread scheduler。

ArenaLiveSession.Dispose 先 runner.Dispose，再 session.Dispose。runner 只釋放驅動權，不會替 caller 釋放 world。

## 設定 Hz、實測 tick/s 與 debt 不同

固定 tick interval 不表示 host 一定有足夠時間達到目標速度。[ArenaPerformanceMetrics](../../Assets/game/arena/src/Unity/ArenaPerformanceMetrics.cs) 是 Unity 外層的觀察工具，每累積至少 .5 秒真實時間更新一次：

- `TARGET Hz` 來自目前 observation 的 `TickDelta`，是設定值。
- `FPS` 是取樣區間的呈現 frame 數除以 wall-clock 秒數。
- `tick/s` 是目前顯示世界的 tick 增量除以相同 wall-clock 秒數；Live、Replay、暫停應分開解讀。
- `live debt` 是 Live runner 已收到、尚未處理完的時間，以 ms 顯示；它包含不足一個 tick 的餘數。Replay 時此欄傳入 0，不是 Replay accumulator 的量測。

這個計數器只讀 `Time.realtimeSinceStartupAsDouble`、tick 與 `PendingSeconds`。它不把量測時間餵回 runner，不重寫 tick rate，也沒有搬到背景 thread。frame 很慢時 FPS 可以下降而 tick/s 暫時維持目標，因為 runner 可能在同一 frame 補跑多 ticks。

目前 ArenaHost 仍以 `Time.deltaTime` 呼叫 AdvanceFrame。這保留 Unity timeScale 與 `Time.maximumDeltaTime` 的政策：長卡頓超過最大 delta 時，Unity 截斷的時間根本沒有傳進 runner，不能從 pending debt 看出或追回。UI 重構並未自動解決這個時間來源問題。[Unity 時間變動說明](https://docs.unity3d.com/6000.3/Documentation/Manual/time-handling-variations.html)

若未來要改成真實時間驅動，應另外決定失焦、暫停、長停頓及補跑上限，補測 input/tick 分配；不能因為換了 UI 就悄悄改變這些語意。效能數字及測量條件記在 [UI Toolkit 驗證報告](../verification/arena-ui-toolkit-2026-08-30.md)，不由本章的設計說明推定改善幅度。

## 執行與反例

```powershell
dotnet run --project tools/arena-checks -- realtime
```

此 selector 驗證 catch-up snapshot pair、按鍵邊緣、清輸入後的新 press、Pause／Resume、錄製重現，以及 manual／realtime drive 互斥。另檢查 budget／明確 Stop／Fault 的 alpha=1、非 owner input 呼叫拒絕及 disposed input 拒絕。框架自身另有 reentry／callback failure 等完整 contract checks；Arena CLI 不應冒充所有 framework 或 Unity 測試。

反例：在建立 runner 後對同 session 呼叫 Simulation.Step，應被拒絕；只 Pause runner 後仍不應取得 manual 權限。另一個反例是把 Attack 寫成「每個 tick 只要按住就送」，長 frame 會連發，與按下邊緣語意不同。

下一章只新增 Unity 這個 host。若發現需要在 MonoBehaviour 重寫距離、扣血或重生，應回到前面的 Application／Integration 補接點，而不是在 Unity 開第二個權威世界。
