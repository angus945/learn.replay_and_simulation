using SimulationCore.Contracts;
using SimulationCore.Logging.API;

namespace SimulationCore
{
    public class SimulationRunner
    {
        ISimulationCommandSystem commandSystem;
        ISimulationExternalCommands externalCommand;
        ISimulationWorld world;
        ISimulationActor actor;
        ISimulationPhysics physics;
        ISimulationPresentation presentation;
        ILogger logger;

        public SimulationRunner(
            ISimulationCommandSystem commandSystem,
            ISimulationExternalCommands commandAcquire,
            ISimulationWorld world,
            ISimulationActor actor,
            ISimulationPhysics physics,
            ISimulationPresentation presentation, float tickDelta = 1f / 60f, ILogger logger = null)
        {
            this.world = world;
            this.externalCommand = commandAcquire;
            this.commandSystem = commandSystem;
            this.actor = actor;
            this.physics = physics;
            this.presentation = presentation;

            this.TickDeltaTime = tickDelta;
            this.logger = logger;
        }

        public readonly float TickDeltaTime;
        public ulong Tick => tick;
        public float Accumulator => (float)accumulator;

        ulong tick;
        double accumulator;

        public void AdvanceTime(float advanceTime)
        {
            accumulator += advanceTime;

            while (accumulator >= TickDeltaTime)
            {
                AdvanceTick();
                accumulator -= TickDeltaTime;
            }
        }
        public void AdvanceTick()
        {
            tick++;
            logger.Trace($"SimulationRunner.AdvanceTick: {tick} | TickDeltaTime: {TickDeltaTime} | Accumulator: {accumulator}", this.GetType().Name);

            // 1. Acquire External Commands
            externalCommand.AcquireCommands(tick, TickDeltaTime);
            commandSystem.DispatchCommands();

            // 2. Pre-Physics Tick
            world.PrePhysicsTick(tick, TickDeltaTime);
            commandSystem.DispatchCommands();

            // 3. Pre-Physics Actor Reconciliation
            // actor.ReconcileBeforePhysics();
            physics.ApplyPrePhysicsState();

            // 4. Physics Simulation
            physics.Simulate(TickDeltaTime);
            physics.CapturePostPhysicsState();
            commandSystem.DispatchCommands();

            // 5. Post-Physics Tick
            world.PrePhysicsTick(tick, TickDeltaTime);
            commandSystem.DispatchCommands();

            // 6. Commit Structural Changes
            world.CommitStructuralChanges();
            actor.ReconcileAfterStructuralCommit();

            // 7. Update Presentation
            presentation.CaptureTickState(tick);
        }
        public void UpdatePresentation()
        {
            presentation.Render();
        }
    }

}
