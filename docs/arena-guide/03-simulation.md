# 03 — Simulation：建立獨立世界與固定 phase

[上一章：Application](02-application.md) · [教材索引](README.md) · [下一章：正式輸入](04-input.md)

本章問題：誰決定一次操作、移動、生命週期提交的順序？若 Unity、測試和 Replay 各自呼叫 Application.Advance，很容易出現不同順序或一個 frame 推進兩次。

這是 deterministic-simulation 的工作：集中 tick、phase、訊息派送與 session 生命週期，遊戲只提供 participants。

## 先建立 scenario 與每次獨立的 runtime

- [ArenaScenario](../../Assets/game/arena/src/Integration/ArenaScenario.cs) 是可序列化的建立配方：tickDelta、seed、遊戲政策與執行預算。`CreateRules()` 只把遊戲政策轉成 Domain 的 ArenaRules。
- [ArenaRuntime](../../Assets/game/arena/src/Integration/ArenaRuntime.cs) 每次 new 出 repository、RegistryLifecycle、SpawnRandom、ArenaApplication。它是 session-owned 資源容器，不是服務定位器，也不是 DDD Aggregate。
- [ArenaDefinition](../../Assets/game/arena/src/Composition/ArenaDefinition.cs) 是最外圍的組裝根；它組合一個私有 `SimulationDefinition<ArenaRuntime, ArenaScenario>`，但本身不繼承通用 testability template。

正式 host 是 [ArenaSession](../../Assets/game/arena/src/Composition/ArenaSession.cs)。它持有唯一 core session，並由 Arena 自己明確編排 control、observation、oracle、trace 與 evidence；被動 modules 不知道下一步該做什麼。

## Definition 接到 framework 的五個生命週期 hooks

以下是 `ArenaDefinition` 類別內的實際成員；它們不是加到 Actor 的方法：

```csharp
protected override void ValidateScenario(ArenaScenario scenario)
    => scenario.Validate();

protected override float GetTickDelta(ArenaScenario scenario)
    => scenario.TickDelta;

protected override ArenaRuntime CreateWorld(ArenaScenario scenario)
    => new ArenaRuntime(scenario);

protected override void Configure(SimulationBuilder builder, ArenaRuntime world, ArenaScenario scenario)
{
    ArenaSimulationWiring.Configure(builder, world);
}

protected override void DestroyWorld(ArenaRuntime world) { }
```

上段所在檔案引用 `Arena.Integration` 與 `DeterministicSimulation.Framework`。Arena 的 `Configure` 明確註冊 input bridge 與其他 participants，沒有隱藏的 testability superclass 接線。

建立順序是驗證設定、建立獨立 world、註冊接點、Build／Seal。Arena 的 world 目前全是 managed、session-owned 物件，所以 DestroyWorld 明確 no-op；未來若有訂閱或外部資源，清理由這個 hook 委派，不放在 Domain。

Definition 只保存不可變的組裝選項，沒有可變 Actor、repository、RNG 或共用 invariant instance。可以重用 Definition，不能重用 world。

## participant 把時間轉成內層需要的參數

[ArenaSimulationWiring](../../Assets/game/arena/src/Integration/ArenaSimulationWiring.cs) 的移動 adapter 很小：

```csharp
private sealed class MovementStep : IPrePhysicsParticipant
{
    private readonly ArenaApplication application;
    public MovementStep(ArenaApplication application)
    {
        this.application = application;
    }
    public void Tick(SimulationContext context)
        => application.Advance(context.Tick.Number, context.Tick.DeltaTime);
}
```

這段位於 Integration 類別內，引用 `Arena.Application` 與 `DeterministicSimulation.Framework`。Configure 以 `builder.RegisterPrePhysicsParticipant(new MovementStep(runtime.Application))` 註冊。

framework 知道 participant；participant 知道 Application；Application 只接收 tick number 與秒數。Actor 不實作 phase interface，也不读取 Time.deltaTime。

## 一個 tick 的固定順序

1. IntentAcquisition：取得由 `ArenaControlAdapter` 排入的 Arena intent。
2. IntentHandling：Input bridge 執行用例，drain commands／events 與後續反應。
3. PrePhysics：MovementStep 推進仍活著的 Actor。
4. Physics：Arena 沒有註冊物理 participant。
5. PostPhysics：Arena 沒有註冊 participant。
6. StructuralCommit：LifetimeCommit 執行移除與到期出生。
7. PresentationCapture：Arena 沒有在 pipeline 註冊呈現 participant。
8. `ArenaSession` 在 pipeline 之後 publish observation、計算 digest、呼叫 oracle set，再保存 tick 證據。

同 phase 的 participants 依註冊順序執行，之後再 drain reactions，不是每個 participant 後各自插入一輪任意排程。Unity 的 snapshot pair／Render 接線在第 9–10 章，不是空的 PresentationCapture phase 自動完成。

`RequireCommand<RespawnCommand>()` 是組裝規格；`RegisterInternalCommandHandler` 才是實際接線。移除 handler 而保留 Require，應在建立 session 時失敗。若只漏掉沒有宣告必需的 event subscriber，framework 不會自動猜出業務缺漏，需用 game acceptance 檢查。

## 手動推進仍使用正式 session

片段放在測試／console 方法中：

```csharp
using System;
using Arena.Composition;
using Arena.Integration;

ArenaDefinition definition = new ArenaDefinition();
using (ArenaSession session = definition.CreateSession(new ArenaScenario(tickDelta: .25f)))
{
    Console.WriteLine(session.CurrentTick); // 0，已 Running
    ArenaTickEvidence tick = session.Step();
    Console.WriteLine(tick.Tick);           // 1
}
```

沒有方向輸入時玩家仍在原點，但 tick 與 phase 已執行。`CreateSession` 回傳 Arena 擁有的正式 host，不存在另一個通用 test session。

## 驗證與生命週期反例

```powershell
dotnet run --project tools/arena-checks -- simulation
```

此 selector 驗證 Submit 不移動、固定 tick 才執行 pipeline，以及 core phase 的正式順序。第 6 章的 observation selector 另驗證兩個 session 互不影響。要重新開始時由 host 建立新的 ArenaSession；Dispose 釋放舊 session，不提供會混用舊 identity 的 Reset。

反序列化工具不一定經過一般 constructor，所以 `CreateSession` 仍會在 core definition 入口重新驗證 scenario，不能只依賴建構時驗證。

反例：在 participant 的 Tick 再呼叫同 session.Step，應被重入保護拒絕。不能把重入當作「補一個 tick」的實作方式。

若 tick 中途拋錯，不承諾 rollback 已修改的 aggregate。低階 runner、SimulationSession 與 ArenaSession 各自有 failure/lifecycle 責任；第 7 章會說明應保存哪個 tick 的證據。下一章先讓外部工具能安全地要求一個未來 tick 的操作。
