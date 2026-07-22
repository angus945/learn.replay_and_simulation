using SimulationInput;
using SimulationInput.Application;
using UnityEngine;

public sealed class SimulationRunner
{
    const float TickRate = 60f;
    const float TickDeltaTime = 1f / TickRate;

    SimulationInputs simulationInputs;
    public SimulationRunner(SimulationInputs simulationInputs)
    {
        this.simulationInputs = simulationInputs;
    }

    public float timeScale = 1f;

    double accumulator;
    ulong tick;

    public void Update(float deltaTime)
    {
        accumulator += deltaTime * timeScale;

        simulationInputs.CaptureRenderInput(new CaptureInputCommand());

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
        TickInputFrame inputFrame = simulationInputs.ConsumeTick(new ConsumeInputCommand(currentTick));

        // 2. 轉換成遊戲意圖 command
        TickInputFrameCommands inputCommands = simulationInputs.ConsumeTickCommands();

        // 2. 執行遊戲邏輯、生成、銷毀、施力
        // 所有操作必須使用穩定順序
        // SimulationWorld.PrePhysicsTick(tick, input);

        // 3. 若有直接修改 Transform，明確同步 //TODO 把 Unity 部分外化
        Physics.SyncTransforms();

        // 4. 推進一次 Unity Physics //TODO 把 Unity 部分外化
        Physics.Simulate(TickDeltaTime);

        // 5. 處理碰撞結果與遊戲狀態
        // SimulationWorld.PostPhysicsTick(tick);

        // 6. 計算 Hash 或儲存 Snapshot
        // ReplayRecorder.RecordTick(tick);

        tick++;
    }

    private void UpdatePresentation()
    {
        // Camera、動畫、粒子、畫面插值等非模擬內容。
    }
}
