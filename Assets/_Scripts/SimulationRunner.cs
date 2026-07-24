// using System;
// using SimulationCore.World.API;
// using Presentation.API;
// using SimulationCore.ExternalCommands.PlayerInput.API;
// using SimulationCore.CommandSystem.API;
// using TickIntentsBuilder.API;
// using TickPhysicsSystem;
// using UnityEngine;

// internal sealed class SimulationRunner
// {
//     const float TickRate = 60f;
//     const float TickDeltaTime = 1f / TickRate;

//     readonly ISimulationCore.ExternalCommands.PlayerInputRuntime SimulationCore.ExternalCommands.PlayerInputs;
//     readonly ITickIntentsBuilder tickIntentsBuilder;
//     readonly IEcsSystemRuntime systemRuntime;
//     readonly ITickCommandDispatcher commandSystem;
//     readonly IPhysicsRuntime physicsRuntime;
//     readonly IEntityActorBridge actorBridge;
//     readonly ISimulationPresentation presentation;

//     public SimulationRunner(
//         ISimulationCore.ExternalCommands.PlayerInputRuntime inputs,
//         ITickIntentsBuilder tickIntentsBuilder,
//         IEcsSystemRuntime systemRuntime,
//         ITickCommandDispatcher commandSystem,
//         IPhysicsRuntime physicsRuntime,
//         IEntityActorBridge actorBridge,
//         ISimulationPresentation presentation)
//     {
//         this.SimulationCore.ExternalCommands.PlayerInputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
//         this.tickIntentsBuilder = tickIntentsBuilder ?? throw new ArgumentNullException(nameof(tickIntentsBuilder));
//         this.systemRuntime = systemRuntime ?? throw new ArgumentNullException(nameof(systemRuntime));
//         this.commandSystem = commandSystem ?? throw new ArgumentNullException(nameof(commandSystem));
//         this.physicsRuntime = physicsRuntime ?? throw new ArgumentNullException(nameof(physicsRuntime));
//         this.actorBridge = actorBridge ?? throw new ArgumentNullException(nameof(actorBridge));
//         this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
//     }

//     public float timeScale = 1f;

//     double accumulator;
//     ulong tick;

//     public void Update(float deltaTime)
//     {
//         accumulator += deltaTime * timeScale;

//         SimulationCore.ExternalCommands.PlayerInputs.CaptureRenderInput();

//         while (accumulator >= TickDeltaTime)
//         {
//             ExecuteTick();
//             accumulator -= TickDeltaTime;
//         }

//         // 只處理渲染插值，不修改 Simulation State。
//         UpdatePresentation();
//     }

//     //TODO collect physics facts
//     //TODO event system
//     //TODO record and replay
//     private void ExecuteTick()
//     {
//         ulong currentTick = tick;

//         // Acquire External Intents
//         IInputSnapshot snapshot = SimulationCore.ExternalCommands.PlayerInputs.ConsumeSnapshot(currentTick);
//         tickIntentsBuilder.ProduceInputCommands(snapshot);
//         tickIntentsBuilder.CommitTick(currentTick);
//         tickIntentsBuilder.EnqueueCommittedCommands(commandSystem);
//         commandSystem.DispatchCommands();

//         // Pre-Physics Gameplay
//         systemRuntime.PrePhysicsTick(currentTick, TickDeltaTime);
//         commandSystem.DispatchCommands();

//         actorBridge.ReconcileBeforePhysics(currentTick, TickDeltaTime);

//         // Physics Step 
//         physicsRuntime.Simulate(TickDeltaTime);

//         // Collect Physics Facts
//         // TODO 收集物理資訊，並轉換成 ECS 事件或命令。

//         // Post-Physics Gameplay
//         systemRuntime.PostPhysicsTick(currentTick, TickDeltaTime);
//         commandSystem.DispatchCommands();

//         // End-Tick Structural Commit.
//         // Spawn requests made during tick T become alive after this point,
//         // so they first participate in gameplay systems on tick T+1.
//         systemRuntime.CommitStructuralChanges();
//         actorBridge.ReconcileAfterStructuralCommit(currentTick, TickDeltaTime);

//         // 10. Finalize Simulation State
//         // 11. Hash / Snapshot
//         // 12. Extract Presentation Events

//         // 13. Update Presentation
//         presentation.UpdatePresentation(currentTick);

//         tick++;
//     }

//     private void UpdatePresentation()
//     {
//         // Camera、動畫、粒子、畫面插值等非模擬內容。
//     }
// }
