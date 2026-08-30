# 05 — Lifecycle：事件、重生排程與 RNG

[上一章：正式輸入](04-input.md) · [教材索引](README.md) · [下一章：Observation／hash](06-observation.md)

本章問題：死亡已成立，但哪個時候移除角色？重生要抽哪個亂數、等到哪個 tick？若在每 frame 任意 Destroy／Instantiate，headless 與 Replay 就沒有同一條生命週期。

這一章保留 Domain 的立即一致性，將後續工作經由 framework 的 event／command 和 StructuralCommit 接到 Application 政策。

## 先分清楚三種資料

- `ArenaInput(Attack, ...)`：外部要求，需錄製。
- `ArenaFact(Defeated, ...)`：Application 回傳的已發生事實，沒有 framework interface。
- `ArenaFactMessage : IDomainEvent` 與 `RespawnCommand : IInternalCommand`：Integration 用來排序與反應的框架訊息，重播時重新推導，不是外部輸入。

相關接線都在 [ArenaSimulationWiring](../../Assets/game/arena/src/Integration/ArenaSimulationWiring.cs)。名稱是 IDomainEvent 並不表示 Domain assembly 必須引用這個介面；Arena 用外圍 message 包裝純遊戲 fact。

## 先把 fact 映射成事件，保留因果關係

這段位於 Integration.Execute，`result` 來自 Application.Execute，`context` 來自 framework 的 input bridge：

```csharp
foreach (ArenaFact fact in result.Facts)
{
    context.Events.PublishDomainEvent(new ArenaFactMessage(
        fact, context.Sequence, context.TargetTick));
}
```

額外保存 sequence／tick 是為了後續 trace 能連回原操作。不要把 context 或 trace recorder 注入 Actor；fact 仍只有角色、目標、傷害與種類。

接著在 Configure 建立同一個 reaction，註冊兩種不同責任：

```csharp
DefeatReaction reaction = new DefeatReaction(
    runtime, builder.Commands, builder.Events);
builder.RequireCommand<RespawnCommand>();
builder.RegisterDomainEventHandler<ArenaFactMessage>(reaction);
builder.RegisterInternalCommandHandler<RespawnCommand>(reaction);
```

這是 ArenaSimulationWiring 內的片段，reaction 為它的私有 adapter。Defeated handler 呼叫 `bool respawn = runtime.Application.OnDefeated(message.Fact.Target)`；Application 要求移除並判斷此角色是否應重生，Integration 只依這個 bool enqueue RespawnCommand，不自己判斷敵人政策。command handler 呼叫 Application.ScheduleRespawn，以當次輸入 tick 為排程基準。

## 完整死亡路徑與可見時機

```text
到期 Attack → ArenaApplication.Execute → Actor.TakeDamage
  → Health=0，方向立即清零
  → ArenaFactMessage(Defeated)
  → OnDefeated：提出 registry destroy
  → RespawnCommand：預約 due tick
PrePhysics：死者不再移動
StructuralCommit：移除死者；生成到期敵人；再提交出生
Testability：capture 新的活動成員 snapshot
```

同 tick 隨後再 Attack 死者會回 target-dead。到了 commit 後它已從 repository 移除，下一 tick 使用舊 ID 則回 target-not-found。Arena 不保留 tombstone；需要死亡歷史請讀結果／trace，不從目前 Actors 清單尋找死者。

新敵人於到期 tick 的 commit 後可見，下一 tick 才可參與輸入與移動。即使 delay=0，也不會倒回這一 tick 已結束的 input／movement phase。

`ArenaApplication.Commit` 先提交移除，再生成到期敵人，再提交出生。這是兩次有序 commit，不是原子交易；中間拋錯沒有 rollback 保證。

## 接 RNG port，而不是在規則裡讀 UnityEngine.Random

[SpawnRandom](../../Assets/game/arena/src/Infrastructure/SpawnRandom.cs) 實作 ISpawnRandom：

```csharp
public SpawnRandom(ulong seed)
{
    health = SplitMix64Random.FromStream(seed, 1);
    delay = SplitMix64Random.FromStream(seed, 2);
}
```

這是 SpawnRandom 類別內的實際片段，`health`、`delay` 為 SplitMix64Random 欄位。stream 1 只在敵人實際生成時抽血量；stream 2 只在成功預約重生時抽延遲。

Application 先檢查 `EnemiesSpawned + PendingRespawnTicks.Count` 是否已占滿出生預算，再抽 delay。錯誤攻擊、重複攻擊死者、超出預算及讀取 observation 都不應額外抽樣。

Domain 的 delay 政策直接使用 tick 數。預設 30–90 ticks，在 60 Hz 下是 .5–1.5 simulation 秒；改 TickDelta 而保留 tick 範圍，秒數會改變。這裡沒有 wall-clock timer，也不是固定 1–3 秒的政策。

bounded integer 的無偏取樣可能使用多次原始 RNG draw，不能假定一次 NextHealth 等於底層只前進一次。下一章把兩條 stream state 納入 canonical state。

## 用固定延遲看清楚邊界

以下片段放在測試／console 方法中。固定 delay 2 ticks 是測試條件，不是 production 新規則：

```csharp
using System;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;
using Testability.Templates;

ArenaScenario scenario = new ArenaScenario(tickDelta: .25f,
    damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2,
    maxEnemySpawns: 2);
using (TestableSimulationSession<ArenaRuntime, ArenaScenario,
    ArenaInput, ArenaObservation> session =
    new ArenaDefinition().CreateTestSession(scenario))
{
    ulong player = session.Observe().PlayerId;
    ulong enemy = 0;
    foreach (ActorSnapshot actor in session.Observe().Actors)
        if (actor.Enemy) { enemy = actor.Id; break; }

    session.Gameplay.Submit(session.Id, 1, 1,
        new ArenaInput(ArenaAction.Attack, player, enemy));
    session.Simulation.Step();
    Console.WriteLine(session.Observe().Actors.Count); // 1
    Console.WriteLine(session.Observe().PendingRespawnTicks[0]); // 3
    session.Simulation.Step(); // tick 2，仍等待
    session.Simulation.Step(); // tick 3，commit 出生
    Console.WriteLine(session.Observe().EnemiesSpawned); // 2
    Console.WriteLine(session.Observe().FindActor(enemy) == null); // True
}
```

ActorId 單調增加，不重用舊遊戲 ID。registry slot 可重用並更新 generation；Unity pool 也有自己的 instance generation。三種身分不能互相替代。[RegistryLifecycle](../../Assets/game/arena/src/Infrastructure/RegistryLifecycle.cs) 保有映射，Domain 不知道 handle。

## 執行與反例

```powershell
dotnet run --project tools/arena-checks -- lifecycle
```

預期驗證死亡到提交的時機、到期重生、舊 ID 不指向新敵人、相同 seed、獨立 streams、預約預算及重生排程。這裡不需要 Unity GameObject。

反例：只移除 Defeated event handler，Attack 仍可能回 defeated，但死者無法按要求清理，post-tick oracle／生命週期檢查應失敗。框架允許 event 沒有 subscriber，因此這是 game 接線驗收責任，不是框架可以自動猜出的缺漏。

下一章將目前活動角色、遊戲 ID 配發進度、RNG 和待重生排程變成明確的 snapshot；只 hash 畫面上的 X/Y 不能證明此生命週期可重現。
