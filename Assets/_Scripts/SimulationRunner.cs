using ECSManagement.API;
using ExternalIntent.API;
using ExternalIntent.Contract;
using SimulationInput.API;
using UnityEngine;

internal sealed class SimulationRunner
{
    const float TickRate = 60f;
    const float TickDeltaTime = 1f / TickRate;

    readonly ISimulationInputRuntime simulationInputs;
    readonly IIntentProducer intentAcquire;
    readonly IEcsSystemRuntime systemRuntime;

    public SimulationRunner(ISimulationInputRuntime inputs, IIntentProducer intentAcquire, IEcsSystemRuntime systemRuntime)
    {
        this.simulationInputs = inputs;
        this.intentAcquire = intentAcquire;
        this.systemRuntime = systemRuntime;
    }

    public float timeScale = 1f;

    double accumulator;
    ulong tick;

    public void Update(float deltaTime)
    {
        accumulator += deltaTime * timeScale;

        simulationInputs.CaptureRenderInput();

        while (accumulator >= TickDeltaTime)
        {
            ExecuteTick();
            accumulator -= TickDeltaTime;
        }

        // 只處理渲染插值，不修改 Simulation State。
        UpdatePresentation();
    }

    private void ExecuteTick()
    {
        ulong currentTick = tick;

        // 1. 讀取此 Tick 已記錄的輸入
        IInputSnapshot snapshot = simulationInputs.ConsumeSnapshot(currentTick);

        // 2. Acquire Intents
        intentAcquire.ProduceInputIntent(snapshot);
        intentAcquire.CommitTick(currentTick);

        // 3. Handle Intents
        systemRuntime.HandleIntents(currentTick, intentAcquire);

        // 2. 執行遊戲邏輯、生成、銷毀、施力
        // 所有操作必須使用穩定順序
        systemRuntime.PrePhysicsTick(currentTick, TickDeltaTime);

        // 3. 若有直接修改 Transform，明確同步 //TODO 把 Unity 部分外化
        Physics.SyncTransforms();

        // 4. 推進一次 Unity Physics //TODO 把 Unity 部分外化
        Physics.Simulate(TickDeltaTime);

        // 5. 處理碰撞結果與遊戲狀態
        systemRuntime.PostPhysicsTick(currentTick, TickDeltaTime);

        // 6. 計算 Hash 或儲存 Snapshot
        // ReplayRecorder.RecordTick(tick);

        tick++;
    }

    private void UpdatePresentation()
    {
        // Camera、動畫、粒子、畫面插值等非模擬內容。
    }
}
