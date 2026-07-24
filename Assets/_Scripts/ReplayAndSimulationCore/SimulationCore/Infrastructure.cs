using SimulationCore.Contracts;

namespace SimulationCore.Infrastructure
{
    public class NullSimulationCommandSystem : ISimulationCommandSystem
    {
        public void DispatchCommands()
        {
            // No-op
        }
    }
    public class NullSimulationExternalCommands : ISimulationExternalCommands
    {
        public void AcquireCommands(ulong tick, float delta)
        {
            // No-op
        }
    }
    public class NullSimulationWorld : ISimulationWorld
    {
        public void PrePhysicsTick(ulong tick, float delta)
        {
            // No-op
        }

        public void CommitStructuralChanges()
        {
            // No-op
        }
    }
    public class NullSimulationActor : ISimulationActor
    {
        public void ReconcileBeforePhysics()
        {
            // No-op
        }

        public void ApplyPrePhysicsState()
        {
            // No-op
        }

        public void ReconcileAfterStructuralCommit()
        {
            // No-op
        }
    }
    public class NullSimulationPhysics : ISimulationPhysics
    {
        public void ApplyPrePhysicsState()
        {
            // No-op
        }

        public void Simulate(float deltaTime)
        {
            // No-op
        }

        public void CapturePostPhysicsState()
        {
            // No-op
        }
    }
    public class NullSimulationPresentation : ISimulationPresentation
    {
        public void CaptureTickState(ulong tick)
        {
            // No-op
        }

        public void Render()
        {
            // No-op
        }
    }

}