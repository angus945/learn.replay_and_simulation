using System;
using ECSManagement.API;
using SimulationInput.API;
using TickCommandSystem.API;
using TickIntentsBuilder.API;
using UnityEngine;

internal sealed class SimulationRunner
{
    const float TickRate = 60f;
    const float TickDeltaTime = 1f / TickRate;

    readonly ISimulationInputRuntime simulationInputs;
    readonly ITickIntentsBuilder tickIntentsBuilder;
    readonly IEcsSystemRuntime systemRuntime;
    readonly ITickCommandDispatcher commandSystem;

    public SimulationRunner(
        ISimulationInputRuntime inputs,
        ITickIntentsBuilder tickIntentsBuilder,
        IEcsSystemRuntime systemRuntime,
        ITickCommandDispatcher commandSystem)
    {
        this.simulationInputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        this.tickIntentsBuilder = tickIntentsBuilder ?? throw new ArgumentNullException(nameof(tickIntentsBuilder));
        this.systemRuntime = systemRuntime ?? throw new ArgumentNullException(nameof(systemRuntime));
        this.commandSystem = commandSystem ?? throw new ArgumentNullException(nameof(commandSystem));
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

    //TODO instantiate unity objects
    //TODO collect physics facts
    //TODO record and replay
    private void ExecuteTick()
    {
        ulong currentTick = tick;

        // Acquire External Intents
        IInputSnapshot snapshot = simulationInputs.ConsumeSnapshot(currentTick);
        tickIntentsBuilder.ProduceInputCommands(snapshot);
        tickIntentsBuilder.CommitTick(currentTick);

        // External commands enter the same route as internal tick commands.
        tickIntentsBuilder.EnqueueCommittedCommands(commandSystem);
        commandSystem.DispatchCommands();

        // Pre-Physics Gameplay
        systemRuntime.PrePhysicsTick(currentTick, TickDeltaTime);
        commandSystem.DispatchCommands();

        // Physics Step //TODO 把 Unity 部分外化
        Physics.SyncTransforms();
        Physics.Simulate(TickDeltaTime);

        // Collect Physics Facts
        // TODO 收集物理資訊，並轉換成 ECS 事件或命令。

        // Post-Physics Gameplay
        systemRuntime.PostPhysicsTick(currentTick, TickDeltaTime);
        commandSystem.DispatchCommands();

        // End-Tick Structural Commit.
        // Spawn requests made during tick T become alive after this point,
        // so they first participate in gameplay systems on tick T+1.
        systemRuntime.CommitStructuralChanges();

        // 10. Finalize Simulation State
        // 11. Hash / Snapshot
        // 12. Extract Presentation Events

        tick++;
    }

    private void UpdatePresentation()
    {
        // Camera、動畫、粒子、畫面插值等非模擬內容。
    }
}
