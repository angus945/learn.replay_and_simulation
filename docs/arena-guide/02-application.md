# 02 — Application：用例與向內定義的 ports

[上一章：Domain](01-domain.md) · [教材索引](README.md) · [下一章：Simulation](03-simulation.md)

本章問題：外部只有 actor ID 與一個操作，誰找出正確 aggregate、判斷距離並回覆拒絕？Actor 不應知道其他角色，也不應知道 registry 或 RNG 實作。

答案是 Application service 加上由內層定義的 ports。內層說明需要什麼能力，外層選擇用哪個 module 完成。

## 本章新增哪些接點

- [ArenaContracts.cs](../../Assets/game/arena/src/Application/ArenaContracts.cs)：`ArenaRequest`、`ArenaResult` 與不可變 `ArenaFact`。沒有 SessionId、TargetTick、trace 或 framework interface。
- [Ports.cs](../../Assets/game/arena/src/Application/Ports.cs)：`IActorRepository`、`IActorLifecycle`、`ISpawnRandom`。
- [ArenaApplication.cs](../../Assets/game/arena/src/Application/ArenaApplication.cs)：正式 Move／Attack 用例、出生政策、遊戲 ID 與重生排程。
- [Application asmdef](../../Assets/game/arena/src/Application/Game.Arena.Application.asmdef) 及 [.NET project](../../tools/arena-build/Game.Arena.Application/Game.Arena.Application.csproj)：只引用 Domain。

這裡不為每個類別建立介面。ports 是為了反轉「需要儲存／隨機／commit 能力」的依賴，並不是讓 Application 自己也藏在一排無意義介面後面。

## 先定義需求，不在內層選擇實作

`ISpawnRandom` 使用有目的的名稱，讓讀程式的人知道哪個規則消耗亂數：

```csharp
namespace Arena.Application
{
    public interface ISpawnRandom
    {
        int NextHealth(int min, int maxInclusive);
        int NextDelay(int min, int maxInclusive);
    }
}
```

Application 不知道 SplitMix64，也不選 stream ID。`IActorRepository.ReadOrdered` 要求穩定遊戲 ID 順序；`IActorLifecycle` 將「要求生成／移除」與「結構提交」分開。

三個 adapter 在 [Infrastructure](../../Assets/game/arena/src/Infrastructure/)：

- ActorRepository 以 SortedDictionary 保存 aggregates。
- RegistryLifecycle 將 ActorId 映射到 registry 身分，commit 時同步移除 repository 成員。
- SpawnRandom 提供 seed 與兩個獨立 stream。

測試可以用簡單 fake 實作相同 ports，正式 session 則選這三個 adapter。這是依賴反轉；不是 mock framework 的要求。

## 把同一批物件注入用例

以下片段放在外圍組裝／測試方法中，不能搬進 Domain。它展示 constructor injection，無需 DI container：

```csharp
using System;
using Arena.Application;
using Arena.Domain;
using Arena.Infrastructure;

ActorRepository repository = new ActorRepository();
RegistryLifecycle lifecycle = new RegistryLifecycle(repository);
SpawnRandom random = new SpawnRandom(814731);
ArenaApplication application = new ArenaApplication(
    repository, lifecycle, random, new ArenaRules());

ArenaResult result = application.Execute(new ArenaRequest(
    ArenaAction.Move, application.PlayerId, x: 1f));
Console.WriteLine(result.Code); // moved，位置仍未推進
application.Advance(1, .25f);    // 第 1 個 tick，玩家 X=1
```

Application 建構時要求空 repository，建立玩家與初始敵人，再提交初始結構。每個 session 都必須重新建立這一組物件；只換一個 SessionId 而沿用原 repository 並不叫隔離。

這段是內層用例測試，不是供 Unity 或外部工具直接操作世界的入口。第 4 章之後，正式 host 只走 `Gameplay.Submit`；Application 的公開方法仍是受信任整合程式的內部邊界，不是安全沙箱。

## 一次 Attack 怎麼執行

`Execute` 依序檢查：

1. action 種類、非零 actor ID、方向值是否有限。
2. actor 是否存在且活著。
3. Attack 的 target 是否有效、不是自己、存在且活著。
4. 用 Domain Position 算距離，是否在 Rules.AttackRange 內。
5. 呼叫 target.TakeDamage，再把實際傷害變成 ArenaFact。

`ArenaResult` 有三種 decision：Accepted、Rejected、InvalidRequest。`actor-not-found`、`out-of-range` 是可預期拒絕，不應終止 session；非法方向是 InvalidRequest。這些都不是 admission 結果，下一章之後才有框架 envelope。

致死 Attack 產生 Damaged 與 Defeated facts，但不直接呼叫 framework dispatcher，也不在 Execute 中移除 repository。事實已成立；後續清理與重生要在第 5 章的反應流程中接上。

## 生命週期政策與機制不要顛倒

Application 擁有這些遊戲狀態：Tick、LastActorId、EnemiesSpawned、PendingRespawnTicks。它決定敵人在 `(1,0)` 出生、出生預算包括預約、延遲到哪個 tick。

RegistryLifecycle 只知道如何將生成／移除套到 registry 和 repository，不決定誰該重生。`ArenaRuntime` 日後持有 adapter 與 Application，不是新的 Aggregate，也不擁有另一份玩家血量。

## 執行、預期與反例

```powershell
dotnet run --project tools/arena-checks -- application
dotnet build tools/arena-build/Game.Arena.Application/Game.Arena.Application.csproj
```

本章片段的 Move 回 moved；CLI 的 application selector 則用致死 Attack 示範回傳 facts，再由測試明確接 OnDefeated／ScheduleRespawn／Advance／Commit，驗證 due tick 與新身分。[ArenaApplicationTests](../../Assets/game/arena/tests/Application/ArenaApplicationTests.cs) 另以 fake ports 驗證內層用例、拒絕不扣血／不多抽亂數、預約上限和排程副本，需獨立執行 NUnit 才能取得該層結果。

反例：若在 Application 加入 `using Testability.Templates`，現行 Application project 因沒有這項引用而無法編譯。不要「修好」為新增 framework reference；應將 mapping 放到 Integration。

另一個反例是完成致死 Attack 後立即要求 Actors.Count 減少；尚未接反應／commit，這個期待應失敗。下一章先建立固定 phase，第 5 章再把 Defeated 事實接到該邊界。
