using ExternalIntent.Contract;

namespace ExternalIntent.Application
{
    internal sealed class AcquireIntentUseCase
    {
        readonly IntentAcquirerStats stats;

        internal AcquireIntentUseCase(IntentAcquirerStats stats)
        {
            this.stats = stats;
        }

        internal IExternalIntent Execute(ulong tick, int index)
        {
            return stats.IntentBuffer.Acquire(tick, index);
        }
    }
}
