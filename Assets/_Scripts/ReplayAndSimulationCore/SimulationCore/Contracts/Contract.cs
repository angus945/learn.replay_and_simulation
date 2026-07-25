namespace SimulationCore.Contracts
{
    public interface ISimulationCommandSystem
    {
        void DispatchCommands();
    }
    public interface ISimulationExternalCommands
    {
        void AcquireCommands(ulong tick, float delta);
    }
    public interface ISimulationWorld
    {
        void PrePhysicsTick(ulong tick, float delta);
        void PostPhysicsTick(ulong tick, float delta);
        void CommitStructuralChanges();
    }

    public interface ISimulationActor
    {
        void ReconcileBeforePhysics();
        // void ApplyPrePhysicsState();
        void ReconcileAfterStructuralCommit();
    }
    public interface ISimulationPhysics
    {
        void ApplyPrePhysicsState();
        void Simulate(float deltaTime);
        void CapturePostPhysicsState();
    }

    public interface ISimulationPresentation
    {
        void CaptureTickState(ulong tick);
        void Render();
    }
}
