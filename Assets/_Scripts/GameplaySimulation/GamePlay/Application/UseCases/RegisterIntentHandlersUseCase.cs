namespace GamePlay.Application
{
    internal sealed class RegisterIntentHandlersUseCase
    {
        readonly GameplayOrchestratorStats stats;

        internal RegisterIntentHandlersUseCase(GameplayOrchestratorStats stats)
        {
            this.stats = stats;
        }

        internal void Execute()
        {
            for (int i = 0; i < stats.Systems.Count; i++)
            {
                stats.Systems[i].RegisterIntentHandler(stats.IntentRegistry);
            }
        }
    }
}
