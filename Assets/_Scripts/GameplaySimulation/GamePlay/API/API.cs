using GamePlay.Contract;
using ExternalIntent.Contract;
using ExternalIntent.API;

namespace GamePlay.API
{
    public interface IGameplayOrchestrator
    {
        void HandleGameplayIntents(ulong tick, ICommittedIntentReader intents);

    }
    public interface IIntentHandlerRegistry
    {
        void RegisterIntentHandler<TIntent>(IIntentHandler<TIntent> handler) where TIntent : IExternalIntent;
    }
}
