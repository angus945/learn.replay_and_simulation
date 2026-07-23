using ExternalIntent.Contract;
using SimulationInput.API;

namespace ExternalIntent.API
{
    public interface IIntentProducer : ICommittedIntentReader
    {
        void ProduceInputIntent(IInputSnapshot snapshot);
        void CommitTick(ulong tick);
    }

    public interface ICommittedIntentReader
    {
        int IntentCount { get; }
        IExternalIntent AcquireIntent(ulong tick, int index);
    }

    public interface IIntentSink
    {
        void Submit(IExternalIntent intent);
    }
}
