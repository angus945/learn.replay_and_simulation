using SimulationInput.API;

namespace ExternalIntent.Contract
{
    public interface IInputIntentRule
    {
        bool TryProduce(IInputSnapshot snapshot, out IExternalIntent intent);
    }

    public interface IExternalIntent
    {

    }
}
