using SimulationCore.ExternalCommands.Port;

namespace SimulationCore.ExternalCommands.Contract
{
    public interface IExternalCommandProvider
    {
        void EnqueueCommands(ulong tick);
    }
}
