using ExternalIntent.API;
using ExternalIntent.Contract;
using SimulationInput.API;

namespace ExternalIntent.Application
{
    public sealed class IntentAcquirer : IIntentProducer
    {
        readonly IntentAcquirerStats stats;
        readonly RegisterInputIntentUseCase registerInputIntentUseCase;
        readonly ProduceInputIntentUseCase produceInputIntentUseCase;
        readonly CommitTickUseCase commitTickUseCase;
        readonly AcquireIntentUseCase acquireIntentUseCase;

        public IntentAcquirer()
        {
            stats = new IntentAcquirerStats();

            registerInputIntentUseCase = new RegisterInputIntentUseCase(stats);
            produceInputIntentUseCase = new ProduceInputIntentUseCase(stats);
            commitTickUseCase = new CommitTickUseCase(stats);
            acquireIntentUseCase = new AcquireIntentUseCase(stats);
        }

        public int IntentCount => stats.IntentBuffer.CommittedIntentCount;

        public void RegisterInputIntent<TIntent>(IInputIntentRule intentRule)
            where TIntent : struct, IExternalIntent
        {
            registerInputIntentUseCase.Execute<TIntent>(intentRule);
        }

        public void ProduceInputIntent(IInputSnapshot snapshot)
        {
            produceInputIntentUseCase.Execute(snapshot);
        }

        public void CommitTick(ulong tick)
        {
            commitTickUseCase.Execute(tick);
        }

        public IExternalIntent AcquireIntent(ulong tick, int index)
        {
            return acquireIntentUseCase.Execute(tick, index);
        }
    }
}
