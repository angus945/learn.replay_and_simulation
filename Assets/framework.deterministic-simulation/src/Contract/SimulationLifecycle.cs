using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    public enum SimulationPhase
    {
        None,
        IntentAcquisition,
        IntentHandling,
        PrePhysics,
        Physics,
        PostPhysics,
        StructuralCommit,
        PresentationCapture,
        PresentationRender
    }

    public readonly struct SimulationContext
    {
        public SimulationContext(SimulationTick tick, SimulationPhase phase)
        {
            Tick = tick;
            Phase = phase;
        }

        public SimulationTick Tick { get; }
        public SimulationPhase Phase { get; }
    }

    public interface IIntentSource
    {
        void AcquireIntents(SimulationContext context, IIntentSink sink);
    }

    public interface IPrePhysicsParticipant
    {
        void Tick(SimulationContext context);
    }

    public interface IPhysicsParticipant
    {
        void Simulate(SimulationContext context);
    }

    public interface IPostPhysicsParticipant
    {
        void Tick(SimulationContext context);
    }

    public interface IStructuralCommitParticipant
    {
        void Commit(SimulationContext context);
    }

    public interface IPresentationParticipant
    {
        void CaptureTickState(SimulationContext context);
        void Render(SimulationContext context, float interpolationAlpha);
    }

}
