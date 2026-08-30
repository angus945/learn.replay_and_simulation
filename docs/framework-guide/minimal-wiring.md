# 極簡接線：把自己的角色接上 Simulation

[回到索引](README.md) · [Definition／Session 契約](definition-template.md)

以「角色收到向右輸入後，每個 tick 向右移動」為例：遊戲負責角色與規則；framework 負責輸入派送、固定 tick 與 session 生命週期。
本例不需要 Unity、Repository、DI container、Protocol 或 Replay，也不要求 Domain 繼承框架。

## 遊戲自己建立什麼？

| 類別             | 遊戲負責的內容                                                | 繼承／實作                             |
| ---------------- | ------------------------------------------------------------- | -------------------------------------- |
| Player           | 位置、持續方向、速度與移動規則                                | 無                                     |
| GameWorld        | 持有這次遊戲的 Player；擴充後可持有 services、repository、RNG | 無                                     |
| MoveInput        | 表達玩家的移動意圖                                            | IIntent                                |
| MoveInputHandler | 將意圖交給 Player；大型玩法可改交給 Application               | IIntentHandler<MoveInput>              |
| MovementTick     | 在 PrePhysics 呼叫移動規則                                    | IPrePhysicsParticipant                 |
| GameDefinition   | 驗證設定、建立世界、註冊接線與清理                            | SimulationDefinition<GameWorld, float> |
| PlayerObserver   | 讀出位置值，不洩漏可變世界                                    | ISimulationObserver<GameWorld, float>  |

Player／GameWorld 不必實作 framework 介面。MoveInput、handler、participant 與 Definition 是外圍整合層。
下面依建立順序拆開說明，每一步可以放在獨立的 C# 檔案；正式遊戲可將 Player 放在不依賴 framework 的 Domain assembly。

第二個泛型 float 是本例的 Scenario，表示固定 tick 秒數；正式遊戲可改成自己的 GameScenario，保存初始位置、速度、seed、關卡等設定。

## 步驟 1：準備檔案與依賴

以下逐步拆解 [MinimalWiringExample.cs](../../tools/gameplay-checks/MinimalWiringExample.cs)，保留相同邏輯；該來源會隨純 .NET 檢查編譯並執行。
文件中的類別省略共同 namespace 與外層縮排，可分別放在同名檔案、使用全域 namespace。不需依賴本專案的 CharacterMovement 領域。
使用 Unity asmdef 時，接線 assembly 至少引用 Framework.DeterministicSimulation 與 Module.SimulationPrimitives；也必須保留框架依賴的 Module.WaveDispatcher。

各檔案所需的 using 會在對應步驟列出。若要對照可執行來源，可將這些類別一起放入 MinimalWiringExample namespace，並呼叫 MinimalWiringExample.Example.Run()。

## 步驟 2：建立 Player，先寫遊戲規則

建立 Player.cs。Player 不繼承任何框架型別，只保存位置、持續方向，並計算移動距離。

```csharp
using System;

// Domain: no framework inheritance or Unity dependency.
public sealed class Player
{
    public float X { get; private set; }
    public float Direction { get; private set; }

    public void SetDirection(float direction)
    {
        if (float.IsNaN(direction) || float.IsInfinity(direction)
            || direction < -1f || direction > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        Direction = direction;
    }

    public void Move(float seconds)
    {
        X += Direction * 4f * seconds;
    }
}

```

SetDirection 只改方向；Move 才更新位置。速度固定為每秒 4，是本例的遊戲規則，不是 framework 的設定。

## 步驟 3：建立 GameWorld，持有這次遊戲的物件

建立 GameWorld.cs，不需要額外 using 或 framework 介面。

```csharp
public sealed class GameWorld
{
    public Player Player { get; } = new Player();
}

```

每次建立 GameWorld 都會建立新的 Player。以後要加入敵人、repository 或 RNG，可以讓 World 持有它們，不要放在共用的 Definition 欄位。

## 步驟 4：建立 MoveInput，表達玩家意圖

建立 MoveInput.cs，實作 IIntent，讓 framework 能把它當作外部意圖排隊。

```csharp
using DeterministicSimulation;

public readonly struct MoveInput : IIntent
{
    public MoveInput(float direction) { Direction = direction; }
    public float Direction { get; }
}

```

Direction 為 1 表示向右、0 表示停止、-1 表示向左。這個訊息只攜帶資料，不自行修改 Player。

## 步驟 5：建立 Handler，把意圖交給遊戲

建立 MoveInputHandler.cs，實作 IIntentHandler<MoveInput> 的 Handle 方法。

```csharp
using DeterministicSimulation;

public sealed class MoveInputHandler : IIntentHandler<MoveInput>
{
    private readonly Player player;

    public MoveInputHandler(Player player) { this.player = player; }

    public void Handle(MoveInput input)
    {
        player.SetDirection(input.Direction);
    }
}

```

framework 派送 MoveInput 時才呼叫 Handle。本例直接呼叫 Player；大型玩法可以在這裡改呼叫 Application service，而不改 framework。

## 步驟 6：建立 MovementTick，接上固定更新階段

建立 MovementTick.cs，實作 IPrePhysicsParticipant 的 Tick 方法。

```csharp
using DeterministicSimulation.Framework;

public sealed class MovementTick : IPrePhysicsParticipant
{
    private readonly Player player;

    public MovementTick(Player player) { this.player = player; }

    public void Tick(SimulationContext context)
    {
        player.Move(context.Tick.DeltaTime);
    }
}

```

每次執行 PrePhysics，framework 就呼叫 Tick，再由 adapter 呼叫 Player.Move。
直接讀取 context.Tick.DeltaTime，不另保存一份 tick 秒數，避免兩份時間設定不一致。Player 本身仍不需要知道 simulation phase。

## 步驟 7：建立 Definition，把前面的類別接起來

建立 GameDefinition.cs，繼承 SimulationDefinition<GameWorld, float>，實作五個 abstract 方法。
這裡的 float 就是初始化時傳入的固定 tick 秒數。

```csharp
using System;
using DeterministicSimulation.Framework;

public sealed class GameDefinition : SimulationDefinition<GameWorld, float>
{
    protected override void ValidateScenario(float tickDelta)
    {
        // The framework also checks that tickDelta is finite and positive.
        if (tickDelta > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDelta));
        }
    }

    protected override float GetTickDelta(float tickDelta) => tickDelta;
    protected override GameWorld CreateWorld(float tickDelta) => new GameWorld();

    protected override void Configure(SimulationBuilder builder, GameWorld world, float tickDelta)
    {
        builder.RequireIntent<MoveInput>();
        builder.RegisterIntentHandler(new MoveInputHandler(world.Player));
        builder.RegisterPrePhysicsParticipant(new MovementTick(world.Player));
    }

    protected override void DestroyWorld(GameWorld world)
    {
        // Managed objects only; no subscriptions or external resources.
    }
}

```

CreateWorld 負責建立遊戲世界；Configure 把同一個 Player 交給輸入 handler 與 tick adapter，確保它們操作的是同一份權威狀態。

| 必填方法         | 責任                                                      |
| ---------------- | --------------------------------------------------------- |
| ValidateScenario | 遊戲設定驗證；框架另檢查 tick 秒數有限且大於零            |
| GetTickDelta     | 回傳固定 tick 秒數                                        |
| CreateWorld      | 每次建立全新的世界，不共用上次 session 的可變資料         |
| Configure        | 註冊 handlers 與 phase participants；框架隨後 Build／Seal |
| DestroyWorld     | 釋放資源與解除訂閱；純 managed world 可以明確 no-op       |

RequireIntent 宣告「這個訊息必須接好」，RegisterIntentHandler 才是實際接線。只有 Require 而沒有 handler，建立 Session 時就會失敗。
Definition 可以共用，但不要在 Definition 欄位保存每個 session 的 Player 或其他可變狀態。

## 步驟 8：建立 Observer，提供唯讀結果

建立 PlayerObserver.cs，實作 ISimulationObserver<GameWorld, float>。

```csharp
using DeterministicSimulation.Framework;

public sealed class PlayerObserver : ISimulationObserver<GameWorld, float>
{
    public float Observe(GameWorld world) => world.Player.X;
}

```

Observe 只回傳位置值，不回傳 Player 物件，避免呼叫端取得可變世界並繞過正式輸入路徑。正式遊戲可改回傳不可變 Snapshot。

## 步驟 9：建立 Session，送輸入並推進

建立 Example.cs，呼叫 Example.Run() 即可驗證前面的接線。Require 只是本例的檢查 helper，不是 framework API。

```csharp
using System;
using DeterministicSimulation.Framework;

public static class Example
{
    public static void Run()
    {
        GameDefinition definition = new GameDefinition();
        PlayerObserver observer = new PlayerObserver();

        using (SimulationSession<GameWorld, float> session =definition.CreateSession(0.25f))
        {
            session.EnqueueIntent(new MoveInput(1f));
            Require(session.Observe(observer) == 0f, "Input only queues.");

            session.Step();
            Require(session.Observe(observer) == 1f, "First tick: X = 1.");

            session.Step();
            Require(session.Observe(observer) == 2f, "Direction persists.");

            session.EnqueueIntent(new MoveInput(0f));
            session.Step();
            Require(session.Observe(observer) == 2f, "Zero direction stops.");

            session.EnqueueIntent(new MoveInput(1f));
            session.Reset(0.25f);
            session.Step();
            Require(session.Observe(observer) == 0f, "Reset replaces world and queue.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
```

using 區塊結束時會 Dispose Session。Reset 則會清掉舊世界與 queue，再依同一個 Definition 建立新世界。

## 步驟 10：確認輸入到位置的執行順序

```text
EnqueueIntent(MoveInput(1))
  → 只排隊，此時 X 仍是 0
Step()
  → IntentHandling：MoveInputHandler 設 Direction = 1
  → PrePhysics：MovementTick 呼叫 Player.Move(0.25)
  → X = 1
Step()
  → 沒有新輸入，Direction 仍是 1
  → PrePhysics 再移動，X = 2
```

速度每秒 4、每 tick 0.25 秒，因此每次前進 1。持續移動是 Player 保存方向的規則，不是 framework 自動補輸入。
送 MoveInput(0) 後，要等下一個 Step 才停止。Reset 則建立全新世界並丟棄舊 queue。

基本 Session 的 EnqueueIntent 沒有 TargetTick／Sequence／ActionResult；這些是下一節延伸模板的能力。
範例對非法方向拋例外，會造成 session Faulted，不是普通業務拒絕；需要明確的拒絕結果時使用延伸模板。
執行錯誤不會 rollback 已完成的世界修改，Faulted 後需 Reset 才能重新推進。

## 如果需要錄製、Replay 與操作結果

改繼承 ReplayableSimulationDefinition<GameWorld, GameScenario, GameInput, GameSnapshot>。
這是另一種 Definition 接法，不是把第二個 host 套在同一個世界上。

四個遊戲型別仍由你建立：World 持有世界，Scenario 保存初始化設定，Input 表達操作，Snapshot 提供不可變觀察。
這條接法的 GameInput 不必實作 IIntent，也不必為外部輸入自行建立 MoveInputHandler；模板提供 Input → Intent → Internal Command 的橋接，呼叫你的 ExecuteInput。

| 必填成員                       | 遊戲要提供的內容                                                       |
| ------------------------------ | ---------------------------------------------------------------------- |
| ValidateScenario／GetTickDelta | 初始化設定驗證與固定秒數                                               |
| CreateWorld／DestroyWorld      | 建立與清理獨立世界                                                     |
| ConfigureWorld                 | 註冊 MovementTick、額外 commands/events、出生移除等 phase participants |
| ExecuteInput                   | 執行操作，例如修改方向，回傳 InputOutcome                              |
| CaptureObservation             | 產生不可變 GameSnapshot                                                |
| EncodeCanonicalState           | 將 snapshot 轉為穩定 bytes，框架計算 hash                              |
| EncodeScenario／DecodeScenario | 保存與重建初始化設定                                                   |
| EncodeInput／DecodeInput       | 保存與重建外部輸入                                                     |
| ConfigureInvariants            | 註冊規則檢查；沒有規則也需明確實作                                     |
| PolicyId                       | 明確識別規則、codec、hash 與 invariant 版本                            |

覆寫 ConfigureWorld，不是基本模板的 Configure；建立時使用 CreateTestSession，不是基本 CreateSession。

使用流程為 Gameplay.Submit(sessionId, sequence, targetTick, input) → Simulation.Step() → Gameplay.Observe()。
Submit 成功只代表排隊，執行結果由 tick 的 ActionResults 或 Results.Find 查詢。
CaptureRecording() 取得錄製，再由同一個 definition.CreateReplay(recording) 建立獨立世界逐 tick 重跑與比對；不需要另寫重播版遊戲規則。

Snapshot／canonical bytes 要涵蓋影響未來的權威狀態，例如持續方向、RNG state、待執行排程，而不只是畫面位置。
Replay 不是完整 snapshot restore 或 rollback，也不保證跨平台 bitwise determinism。

完整實例見 [GameplayDefinition.cs](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)，契約與操作範例見 [Testability／Replay 模板](testability-replay-template.md)。

## 接 Unity 時放在哪一層？

可直接照做的 MonoBehaviour 範例見 [極簡 Unity 接線](minimal-unity-wiring.md)，依步驟將本篇的純 C# 世界接到 Cube。

Unity adapter 擷取輸入，把 frame 時間交給 session.CreateRealtimeRunner 建立的 Runner，由它安排固定 tick，再把唯讀觀察轉成 Transform／插值顯示；也可在不建立 Runner 的情況下保留手動 Step。
不要用 Transform 當權威位置，也不要同時讓 Update adapter、FixedUpdate 或另一個 Runner 推進同一世界。
基本與延伸模板都不會替你自動取樣鍵盤或產生畫面插值。

目前 Demo 的 [MovementDemoSession.cs](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs) 已採用延伸模板；見 [Demo 整合](demo-template.md)。
舊 GameplaySession 是保留給 Protocol 與舊格式相容的路徑，不是目前 Demo 的核心。

## 執行驗證

在 repository 根目錄執行：

```powershell
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

包含本例的輸入只排隊、第一 tick 移動、持續方向、停止、Reset 清世界與 queue 檢查。
修改本頁 C# 範例時，請同步修改 MinimalWiringExample.cs 並重新執行。
