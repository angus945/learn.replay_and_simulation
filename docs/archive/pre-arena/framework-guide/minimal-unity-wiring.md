# 從零建立 Unity Simulation 案例：A／D 移動方塊

[回到索引](README.md)

這是一個獨立、完整的案例，不需要先看其他教學，也不需要複製 tools 裡的範例。
從普通 C# 遊戲物件開始，逐步建立輸入、tick adapter、Definition，最後接上 Unity 與框架的 RealtimeSimulationRunner。

完成後：在場景放一個 Cube，掛上 CubeSimulationHost，按 A／D 左右移動。
遊戲位置由純 C# CubeActor 管理；Unity Transform 只顯示結果。本文不加入 Rigidbody、碰撞、插值或 Replay，文末保留手動控制 Session 的用法。

本次只編寫教學文件，不自動建立下列腳本或場景；此頁程式碼尚未執行 Unity 編譯／Play Mode 驗證。

## 步驟 1：準備資料夾與 framework 依賴

在本專案新增 Assets/game/cube-simulation-example/ 資料夾。
本專案已包含以下 framework／modules，不需要另外安裝：

- Framework.DeterministicSimulation
- Module.SimulationPrimitives
- Module.WaveDispatcher（framework 的依賴）

接下來每個 code block 都會指出放在哪個檔案。除了 Host 分步補入方法之外，每個檔案都提供完整類別與 using。
所有範例類別使用全域 namespace，不引用其他教學的型別。

最簡單可先不建立新的 asmdef，讓 Unity 使用預設 assembly 編譯。若資料夾受自訂 asmdef 管理，該 assembly 須引用 Framework.DeterministicSimulation 與 Module.SimulationPrimitives；Host、鍵盤 adapter 與呈現 adapter 使用 UnityEngine，不能放在 noEngineReferences assembly 中。
若拆成 Domain／Integration／Unity 多個 assembly，外層還需引用內層；本例先不拆，降低組裝步驟。

將建立的檔案與責任：

| 檔案                  | 責任                     | 繼承／實作                             |
| --------------------- | ------------------------ | -------------------------------------- |
| CubeActor.cs          | 位置、方向與移動規則     | 無                                     |
| CubeWorld.cs          | 持有這次遊戲的 CubeActor | 無                                     |
| CubeMoveInput.cs      | 表達移動方向             | IIntent                                |
| CubeMoveHandler.cs    | 將輸入交給遊戲物件       | IIntentHandler<CubeMoveInput>          |
| CubeMovementTick.cs   | 固定 tick 呼叫移動       | IPrePhysicsParticipant                 |
| CubeDefinition.cs     | 建立世界與註冊接線       | SimulationDefinition<CubeWorld, float> |
| CubeObserver.cs       | 回傳唯讀位置值           | ISimulationObserver<CubeWorld, float>  |
| CubeKeyboardInput.cs  | 鍵盤取樣與送出意圖       | IRealtimeInputSource                   |
| CubePresentation.cs   | 捕捉位置與更新 Transform | IRealtimePresentation                  |
| CubeSimulationHost.cs | 組裝、時間轉交與清理     | MonoBehaviour                          |

## 步驟 2：建立遊戲物件 CubeActor

建立 CubeActor.cs。這是遊戲自己的規則，不繼承 framework，也不使用 UnityEngine。

```csharp
using System;

public sealed class CubeActor
{
    public float X { get; private set; }
    public float Direction { get; private set; }

    public void SetDirection(float direction)
    {
        if (float.IsNaN(direction) || float.IsInfinity(direction) || direction < -1f || direction > 1f)
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

速度固定為每秒 4 單位。SetDirection 只改方向，不會立即移動；Move 才更新位置。
Direction 保存在物件內，因此沒有新輸入時仍持續移動。這是遊戲規則，不是 framework 自動補輸入。

## 步驟 3：建立世界 CubeWorld

建立 CubeWorld.cs，持有這次 Session 的遊戲物件。

```csharp
public sealed class CubeWorld
{
    public CubeActor Actor { get; } = new CubeActor();
}
```

每個 CubeWorld 都有自己的 Actor。World 不需繼承 framework；未來可增加敵人、repository、RNG 等遊戲服務。

## 步驟 4：建立移動輸入 CubeMoveInput

建立 CubeMoveInput.cs，實作 IIntent。

```csharp
using DeterministicSimulation;

public readonly struct CubeMoveInput : IIntent
{
    public CubeMoveInput(float direction)
    {
        Direction = direction;
    }

    public float Direction { get; }
}
```

這個訊息只表示「想往哪裡移動」，不直接修改世界。1 是向右、-1 是向左、0 是停止。

## 步驟 5：建立輸入處理器 CubeMoveHandler

建立 CubeMoveHandler.cs，實作 IIntentHandler<CubeMoveInput>。

```csharp
using DeterministicSimulation;

public sealed class CubeMoveHandler : IIntentHandler<CubeMoveInput>
{
    private readonly CubeActor actor;

    public CubeMoveHandler(CubeActor actor)
    {
        this.actor = actor;
    }

    public void Handle(CubeMoveInput input)
    {
        actor.SetDirection(input.Direction);
    }
}
```

framework 在 tick 的 IntentHandling 階段呼叫 Handle。這裡只是把輸入交給遊戲規則，不負責計時。

## 步驟 6：建立固定更新 CubeMovementTick

建立 CubeMovementTick.cs，實作 IPrePhysicsParticipant。

```csharp
using DeterministicSimulation.Framework;

public sealed class CubeMovementTick : IPrePhysicsParticipant
{
    private readonly CubeActor actor;

    public CubeMovementTick(CubeActor actor)
    {
        this.actor = actor;
    }

    public void Tick(SimulationContext context)
    {
        actor.Move(context.Tick.DeltaTime);
    }
}
```

每個 tick 的 PrePhysics 階段，framework 都會呼叫 Tick。
傳給遊戲的是固定 tick 秒數，不是 Unity 的 frame delta。這個 phase 名稱不代表一定要使用物理引擎，本例只有純 C# 移動。

## 步驟 7：建立 Definition，註冊所有接線

建立 CubeDefinition.cs，繼承 SimulationDefinition<CubeWorld, float>。
第二個泛型 float 是本例的初始化設定，代表 tick 秒數；不需要另找其他 Scenario 類別。

```csharp
using System;
using DeterministicSimulation.Framework;

public sealed class CubeDefinition : SimulationDefinition<CubeWorld, float>
{
    protected override void ValidateScenario(float tickDelta)
    {
        if (tickDelta > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDelta));
        }
    }

    protected override float GetTickDelta(float tickDelta)
    {
        return tickDelta;
    }

    protected override CubeWorld CreateWorld(float tickDelta)
    {
        return new CubeWorld();
    }

    protected override void Configure(SimulationBuilder builder, CubeWorld world, float tickDelta)
    {
        builder.RequireIntent<CubeMoveInput>();
        builder.RegisterIntentHandler(new CubeMoveHandler(world.Actor));
        builder.RegisterPrePhysicsParticipant(new CubeMovementTick(world.Actor));
    }

    protected override void DestroyWorld(CubeWorld world)
    {
        // 本例只有 managed 物件，沒有訂閱或外部資源需要釋放。
    }
}
```

五個必填方法的責任：

| 方法             | 本例提供什麼                                                    |
| ---------------- | --------------------------------------------------------------- |
| ValidateScenario | 遊戲額外限制：tick 秒數不可超過 1；framework 另檢查有限且大於零 |
| GetTickDelta     | 固定 tick 秒數                                                  |
| CreateWorld      | 全新的 CubeWorld                                                |
| Configure        | 把同一個 Actor 接給輸入 handler 與 tick adapter                 |
| DestroyWorld     | 清理資源；本例明確留空                                          |

RequireIntent 宣告必要接線，RegisterIntentHandler 才是真正註冊。缺 handler 時，建立 Session 就會失敗。
建立 Session 時 framework 會完成 Build／Seal，不需要遊戲自己建立低階 SimulationRunner。
Definition 可以共用，但不要把每次遊戲的可變 Actor 保存在 Definition 欄位。

## 步驟 8：建立 Observer，讀出位置

建立 CubeObserver.cs，實作 ISimulationObserver<CubeWorld, float>。

```csharp
using DeterministicSimulation.Framework;

public sealed class CubeObserver : ISimulationObserver<CubeWorld, float>
{
    public float Observe(CubeWorld world)
    {
        return world.Actor.X;
    }
}
```

這裡只回傳 float 值，不把可變的 Actor 交給 Unity Host。
讀取不推進 tick，也不應修改世界。

## 步驟 9：建立獨立的鍵盤輸入 adapter

建立 CubeKeyboardInput.cs，只有這個類別實作 IRealtimeInputSource。
它是普通 C# 類別，不是 MonoBehaviour；由 Host 建立並交給 Runner。

```csharp
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using UnityEngine;

public sealed class CubeKeyboardInput : IRealtimeInputSource
{
    private readonly SimulationSession<CubeWorld, float> session;
    private float direction;

    public CubeKeyboardInput(SimulationSession<CubeWorld, float> session)
    {
        this.session = session;
    }

    public void CaptureFrame()
    {
        direction = 0f;

        if (!Application.isFocused) return;

        if (Input.GetKey(KeyCode.A)) direction -= 1f;
        if (Input.GetKey(KeyCode.D)) direction += 1f;
    }

    public void AcquireInput(SimulationTick tick)
    {
        session.EnqueueIntent(new CubeMoveInput(direction));
    }
}
```

兩個方法有不同的呼叫時機：

- Host 每個 Update 呼叫 CaptureFrame：取樣鍵盤並保存方向。
- Runner 每個 tick 前呼叫 AcquireInput：將保存的方向轉成意圖，送進 Session。

A 為 -1、D 為 1；同時按下、都沒按或失去焦點時為 0。
同一 frame 補跑多個 tick 時沿用同一次取樣，這不是帶時間戳的鍵盤事件記錄器。
tick 是即將執行的 tick；基本 Session 的意圖佇列不需另外填入 tick number。adapter 只送輸入，不呼叫 Step。

本例使用傳統 Unity Input API，Active Input Handling 必須支援 Input Manager (Old) 或 Both。
若只使用新版 Input System，替換 CaptureFrame 的鍵盤取樣即可，不需要改 Host 的組裝以外流程或 Domain。

## 步驟 10：建立獨立的呈現 adapter

建立 CubePresentation.cs，只有這個類別實作 IRealtimePresentation。
它透過 Observer 取得位置，不直接取得 CubeActor；Transform 由 Host 注入。

```csharp
using DeterministicSimulation.Framework;
using UnityEngine;

public sealed class CubePresentation : IRealtimePresentation
{
    private readonly SimulationSession<CubeWorld, float> session;
    private readonly CubeObserver observer;
    private readonly Transform target;
    private readonly Vector3 origin;
    private float currentX;

    public CubePresentation(SimulationSession<CubeWorld, float> session, CubeObserver observer, Transform target)
    {
        this.session = session;
        this.observer = observer;
        this.target = target;
        origin = target.position;

        CaptureTickState(session.TickNumber);
    }

    public void CaptureTickState(ulong tick)
    {
        currentX = session.Observe(observer);
    }

    public void Render(float alpha)
    {
        target.position = origin + Vector3.right * currentX;
    }
}
```

Runner 在每個 tick 後呼叫 CaptureTickState，並在 UpdatePresentation 時呼叫 Render。
建構時先捕捉初始位置，因此第一個 tick 尚未執行也能呈現。
origin 是場景初始位置的畫面偏移；CubeActor.X 仍從 0 開始。

本例刻意忽略 alpha，直接顯示最新 tick 位置，沒有自動插值。
之後要加插值，修改這個 adapter 保存前後位置即可，不需要讓 Host 管理畫面狀態。

## 步驟 11：建立只負責組裝的 Host 骨架

新增 CubeSimulationHost.cs。Host 只繼承 MonoBehaviour，不實作任何輸入或呈現介面。
後續步驟的方法都加在同一個類別內，不要重複建立類別。

```csharp
using DeterministicSimulation.Framework;
using UnityEngine;

public sealed class CubeSimulationHost : MonoBehaviour
{
    private const float TickDelta = 1f / 60f;

    private SimulationSession<CubeWorld, float> session;
    private RealtimeSimulationRunner runner;
    private CubeKeyboardInput input;
    private CubePresentation presentation;

    // 在這裡加入步驟 12～14 的方法。
}
```

Host 不保存方向、位置快照或 Observer，也不包含鍵盤判斷與 Transform 更新邏輯。
TickDelta 決定固定 tick 秒數；時間累積交給 Runner。

## 步驟 12：在 Awake 組裝 Session 與 adapters

將以下方法加入 CubeSimulationHost：

```csharp
private void Awake()
{
    CubeDefinition definition = new CubeDefinition();
    session = definition.CreateSession(TickDelta);

    input = new CubeKeyboardInput(session);
    presentation = new CubePresentation(session, new CubeObserver(), transform);

    runner = session.CreateRealtimeRunner(maxTicksPerFrame: 120, input: input, presentation: presentation);
}
```

這裡是唯一的組裝位置：

1. Definition 建立世界與 domain 接線。
2. 建立鍵盤、呈現兩個獨立物件。
3. 將兩個物件注入 Runner，而不是傳入 this。

CreateRealtimeRunner 取得唯一驅動權，持有期間不能再公開呼叫 session.Step()。
低階 tick source 由 Session 自行提供，遊戲端不需要實作它，也不需另建低階 SimulationRunner。

## 步驟 13：Host 轉交 Unity 的更新時機

將以下方法加入同一個 Host：

```csharp
private void Update()
{
    if (runner == null || runner.Failure != null) return;

    input.CaptureFrame();
    runner.AdvanceTime(Time.deltaTime);
}

private void LateUpdate()
{
    if (runner == null || runner.Failure != null) return;

    runner.UpdatePresentation();
}
```

Host 只通知 input 取樣、交出 frame 時間，再通知 Runner 呈現。
它不自行送意圖、不呼叫 Observe，也不更新 Transform。

Runner 依累積時間執行零個、一個或多個 tick，每個 tick 都先呼叫 input.AcquireInput，再推進 Session，最後呼叫 presentation.CaptureTickState。
不要在 Update 中額外 Step，否則會違反唯一驅動權契約。

## 步驟 14：清理生命週期

最後加入 Host：

```csharp
private void OnDestroy()
{
    runner?.Dispose();
    session?.Dispose();
}
```

先釋放 Runner 驅動權，再 Dispose Session。Runner 不會代替你釋放世界。
本例兩個 adapter 沒有事件訂閱或非 managed 資源，不需要額外 Dispose。

## 步驟 15：掛到場景並操作

1. 建立 Cube，確認它在 Camera 可見範圍內。
2. 只把 CubeSimulationHost 掛到 Cube。
3. 不需掛 CubeKeyboardInput／CubePresentation：它們由 Host 用 new 建立，不是元件。
4. 不需要 Rigidbody；本例也不模擬碰撞。
5. 按 Play、點擊 Game View，再按 A／D 左右移動，放開停止。

預期速度每秒 4 單位，固定 tick 為 1/60 秒，每 tick 約移動 0.0667 單位。
停止輸入在下一個 tick 生效。

## 步驟 16：確認各層責任

```text
Host.Update
  → CubeKeyboardInput.CaptureFrame：讀鍵盤
  → Runner.AdvanceTime
      → CubeKeyboardInput.AcquireInput：送意圖
      → Session tick
          → CubeMoveHandler：更新方向
          → CubeMovementTick：更新位置
      → CubePresentation.CaptureTickState：讀取位置快照
Host.LateUpdate
  → Runner.UpdatePresentation
      → CubePresentation.Render：更新 Transform
```

| 物件                       | 責任                             | 不負責                             |
| -------------------------- | -------------------------------- | ---------------------------------- |
| CubeSimulationHost         | 組裝、Unity 更新時機、清理       | 鍵盤規則、位置快照、畫面更新       |
| CubeKeyboardInput          | 取樣鍵盤、每 tick 送意圖         | 推進世界、呈現                     |
| CubePresentation           | 讀取 snapshot、更新 Transform    | 操作角色、送意圖、推進世界         |
| RealtimeSimulationRunner   | 累積時間、安排 tick 與 callbacks | 遊戲規則、直接操作鍵盤或 Transform |
| CubeActor／domain adapters | 方向、移動規則與 tick 接線       | Unity frame 與畫面                 |

本例為了精簡，兩個 adapter 都注入基本 Session；責任分工靠實作約定，不代表它們取得的是受限權限 facade。
CubeActor.X 才是權威位置。Play 中拖動 Cube，下一次呈現會被 snapshot 覆蓋；傳送或出生座標應走遊戲設定／操作流程。

## 替代接法 A：保留手動 Session 控制

單元測試、指定 tick 驗證或逐步除錯，仍可直接使用 Session，不必建立 Realtime Runner。直接使用本頁已建立的 CubeDefinition、CubeObserver 與 CubeMoveInput 即可：

```csharp
// 放在方法內執行；所在檔案加入 using DeterministicSimulation.Framework。
CubeDefinition definition = new CubeDefinition();
CubeObserver observer = new CubeObserver();

using (SimulationSession<CubeWorld, float> manualSession = definition.CreateSession(0.25f))
{
    manualSession.EnqueueIntent(new CubeMoveInput(1f));
    manualSession.Step();
    float x = manualSession.Observe(observer); // X = 1
}
```

這是獨立用法，不要在上方 Host 的每個 Update 建立新 Session。

若要把既有 Host 切到手動模式，請在 Runner callback 之外先停止 Update／LateUpdate 呼叫 Runner，再釋放它：

```csharp
runner?.Dispose();
runner = null;

session.EnqueueIntent(new CubeMoveInput(1f));
session.Step();
presentation.CaptureTickState(session.TickNumber);
presentation.Render(1f);
```

這段放在 Host 的方法內，從 UI 或其他主執行緒入口呼叫，不要放在 Runner callback。上方 Update／LateUpdate 已檢查 runner 是否為 null，因此切換後不會繼續使用舊 Runner。
只呼叫 runner.Pause() 不夠：Pause 仍保留驅動權，session.Step() 仍會拒絕執行。
要回到即時模式，用 session.CreateRealtimeRunner(input: input, presentation: presentation) 重新取得 Runner。
要 Reset 也先釋放 Runner，Reset 後呼叫 presentation.CaptureTickState(session.TickNumber) 重讀位置快照並建立新 Runner，避免沿用舊 tick delta 或畫面資料。

## 替代接法 B：自行累積時間並 Step

若想理解底層時間迴圈，原本的手動版本仍可使用；它與主要 Runner 路徑是二選一，不要同時啟用。
不建立 Runner，增加 private double accumulator 欄位，將 Update 替換成：

```csharp
private void Update()
{
    input.CaptureFrame();
    accumulator += Time.deltaTime;

    while (accumulator >= TickDelta)
    {
        input.AcquireInput(new DeterministicSimulation.SimulationTick(
            session.TickNumber + 1, TickDelta));
        session.Step();
        accumulator -= TickDelta;
    }

    presentation.CaptureTickState(session.TickNumber);
    presentation.Render(1f);
}
```

保留 Awake 建立 input 與 presentation，但移除 CreateRealtimeRunner 與 LateUpdate；銷毀時只需 Dispose Session。手動迴圈仍透過兩個 adapter 取得輸入與呈現，不把這些實作搬回 Host。
這個教學版本沒有單 frame 補跑上限或 runner 的驅動權保護，時間與錯誤處理都由 Host 自己負責。一般即時遊戲優先使用上方框架 Runner。

## Runner 行為與本例省略的部分

- 唯一時間來源：不要再用 FixedUpdate 或另一個 Runner 推進同一個世界。
- 插值：目前直接顯示最新 tick 位置，高畫面更新率下可能看到階梯感；可再保存前後位置並使用 Render(alpha) 的參數插值。
- 時間政策：使用 Time.deltaTime，因此受到 timeScale 影響。Runner 每次 AdvanceTime 最多執行 120 ticks，多餘時間保留在 PendingSeconds，不會自動丟棄 tick；此上限不是 handler 執行時間的 watchdog。
- 暫停：runner.Pause() 清除累積時間但保留驅動權；Resume() 不補跑暫停期間的時間。此頁沒有暫停選單。
- Fault 處理：本例沒有 Reset／錯誤 UI；若 Runner 拋例外，應停止後續 Update／LateUpdate 呼叫，不要每 frame 重試。輸入或呈現錯誤可能只記在 Runner.Failure，不能一律當作 Session Faulted；先釋放失敗 Runner，再依 Session 狀態決定是否 Reset。
- Replay：基本 CreateSession 不提供錄製。需要時改用 [Testability／Replay 模板](testability-replay-template.md)，不要在同一世界再加第二個 host。

需要完整輸入、插值與 Replay 的現有實作，可繼續閱讀 [MovementDemoSession.cs](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs) 與 [Demo 整合](demo-template.md)。
