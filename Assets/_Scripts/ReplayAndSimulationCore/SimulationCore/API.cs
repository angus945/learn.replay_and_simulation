namespace SimulationCore.API
{
    public interface ISimulationRunner
    {
        void AdvanceTime(float advanceTime);
        void UpdatePresentation();
    }
}
