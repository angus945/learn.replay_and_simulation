# 01 — Domain：先讓規則不需要 framework

[教材索引](README.md) · [下一章：Application](02-application.md)

本章問題：移動、扣血與死亡應由誰維持一致？如果答案是 Unity Update 或測試工具，換一個 host 就得重寫規則。

因此先建立純 C# Actor aggregate。它知道自己的身分、位置、方向、速度與血量，不知道 frame、simulation phase、錄製或 trace。

## 本章建立的檔案與理由

- [ActorId.cs](../../Assets/game/arena/src/Domain/ActorId.cs)：遊戲身分 value object。`0` 是無效／未指定值；Actor 建立時才拒絕無效身分。它不是 registry slot 或 Unity instance ID。
- [Position.cs](../../Assets/game/arena/src/Domain/Position.cs)：有限的二維座標值，禁止 NaN／Infinity；也用來表示方向，方向的長度限制由 Actor 負責。
- [Actor.cs](../../Assets/game/arena/src/Domain/Actor.cs)：Aggregate root，封裝移動及血量修改，外部只能讀取狀態。
- [ArenaRules.cs](../../Assets/game/arena/src/Domain/ArenaRules.cs)：不可變的遊戲政策。攻擊傷害、範圍、出生預算、重生 tick 範圍在這裡；trace 容量和 recording 預算不在這裡。
- [Game.Arena.Domain.asmdef](../../Assets/game/arena/src/Domain/Game.Arena.Domain.asmdef)：讓沒有引擎／框架依賴成為編譯限制，不只是口頭約定。

一個 Actor 同時管理移動與生命值，是因為死亡必須立即停止移動。此範例沒有需要分成 Movement／Combat bounded context 的不同語言或一致性邊界。

## 先拆開「要求方向」與「推進時間」

以下片段放在測試／console 方法中；只需引用 Domain：

```csharp
using System;
using Arena.Domain;

Actor player = new Actor(new ActorId(1), ActorKind.Player,
    new Position(0f, 0f), speed: 4f, maxHealth: 30);

player.SetDirection(1f, 0f);
Console.WriteLine(player.Position.X); // 0：只改持續方向
player.Advance(.25f);
Console.WriteLine(player.Position.X); // 1：時間才推進位置
```

Actor 不決定 `.25f` 來自哪裡。現在測試直接傳入，之後由固定 tick participant 傳入；移動公式沒有變。

方向長度大於 1 時正規化，避免 `(1,1)` 比 `(1,0)` 快。長度小於 1 時保留類比幅度，不把搖桿的四分之一推力變成全速。`Advance` 先建立通過驗證的新 Position，最後才替換舊值，避免 X 更新但 Y 驗證失敗的半更新。

## 血量與死亡是 aggregate 自己保護的規則

接續上一段：

```csharp
int applied = player.TakeDamage(100);
Console.WriteLine(applied);          // 30，不把 overkill 當實際傷害
Console.WriteLine(player.Health);    // 0
Console.WriteLine(player.IsDead);    // True
Console.WriteLine(player.Direction.X); // 0

player.Advance(.25f);                // 死亡後不再位移
Console.WriteLine(player.Position.X); // 仍是 1
```

`TakeDamage` 同時更新 Health 與死亡後的 Direction，呼叫者不需再記得清方向。外部沒有 `SetHealth` 或 `SetPosition`，也沒有為測試而打開私有 setter。

正常遊戲裡「攻擊已死目標」由下一章 Application 回傳業務拒絕。Actor 本身只保護本地一致性：例如對死者 `SetDirection` 會拋錯，而重複傷害只套用 0 點。

## 誰呼叫、誰擁有

- 現在由測試直接建立／呼叫 Actor；正式遊戲由 Application 建立並操作它。
- repository 保存 session 內的 aggregates；不使用 static singleton。
- Domain 不發布 framework event。扣血結果到第 2 章才變成 ArenaFact，第 5 章才映射成可派送的訊息。
- Actor 的規則是 DDD invariant。第 7 章的 post-tick oracle 只能偵測漏掉的整合問題，不能代替 Actor 維持一致性。

## 執行與預期

```powershell
dotnet run --project tools/arena-checks -- domain
dotnet build tools/arena-build/Game.Arena.Domain/Game.Arena.Domain.csproj
```

此 selector 檢查方向／時間分離、斜向限速、非法方向、死亡扣血及死後不再位移；成功退出不等於 Unity 已驗證。[ActorTests](../../Assets/game/arena/tests/Domain/ActorTests.cs) 另包含負時間、初始狀態、類比幅度、overflow 與拒絕後不變性等 NUnit 案例，需由相應測試 runner 執行，不能用 CLI 成功替代。

## 反例與小練習

將 `Advance(.25f)` 改為 `.5f`，先預測 X 應為 2。若測試仍要求 1，它應失敗；這是輸入／時間變化，不是 framework 不確定。

再試 `Advance(-1f)` 或 `SetDirection(float.NaN, 0f)`，確認拋錯後原狀態不變。不要為了讓測試通過而把驗證搬到 MonoBehaviour，否則 headless caller 會繞過規則。

本章尚未處理「找哪個角色、攻擊距離、死亡後如何重生」。這些需要跨物件協調，下一章加入 Application，而不是讓 Actor 找全域世界。
