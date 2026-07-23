using SimulationInput.API;
using TickCommandSystem.Contract;

namespace TickIntentsBuilder.Contract
{
    public interface IInputCommandRule
    {
        bool TryProduce(IInputSnapshot snapshot, out ICommand command);
    }
}
