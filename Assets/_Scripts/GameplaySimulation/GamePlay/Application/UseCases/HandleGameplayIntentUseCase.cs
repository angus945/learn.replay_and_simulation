using ExternalIntent.API;
using ExternalIntent.Contract;

namespace GamePlay.Application
{
    internal sealed class HandleGameplayIntentUseCase
    {
        readonly GameplayOrchestratorStats stats;

        internal HandleGameplayIntentUseCase(GameplayOrchestratorStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(ulong tick, ICommittedIntentReader intentReader)
        {
            for (int i = 0; i < intentReader.IntentCount; i++)
            {
                IExternalIntent intent = intentReader.AcquireIntent(tick, i);
                IntentHandlerRegistry intentRegistry = stats.IntentRegistry;
                intentRegistry.HandleIntent(intent);
            }
        }
    }
}
