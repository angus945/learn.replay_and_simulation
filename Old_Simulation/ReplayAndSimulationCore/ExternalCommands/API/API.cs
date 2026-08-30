namespace SimulationCore.ExternalCommands.API
{
    public interface ISimulationExternalCommands : SimulationCore.Contracts.ISimulationExternalCommands
    {
        void AcquireCommands(ulong tick);
    }
}
