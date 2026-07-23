using ExternalIntent.Contract;

namespace ExternalIntent.Application
{
    internal sealed class AcquireIntentUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal AcquireIntentUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal IExternalIntent Execute(ulong tick, int index)
        {
            return stats.IntentBuffer.Acquire(tick, index);
        }
    }
}
