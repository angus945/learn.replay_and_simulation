using System.Collections.Generic;
using GamePlay.API;
using GamePlay.Contract;
using ExternalIntent.Contract;
using ExternalIntent.API;

namespace GamePlay.Application
{
    internal sealed class GameplayOrchestratorStats
    {
        internal readonly List<IGameplaySystem> Systems = new();
        internal readonly IntentHandlerRegistry IntentRegistry = new();
    }
    public sealed class GamePlayOrchestrator : IGameplayOrchestrator
    {
        readonly GameplayOrchestratorStats stats;
        readonly RegisterGameplaySystemUseCase registerGameplaySystemUseCase;
        readonly RegisterIntentHandlersUseCase registerIntentHandlersUseCase;
        readonly HandleGameplayIntentUseCase handleGameplayIntentUseCase;

        public GamePlayOrchestrator()
        {
            stats = new GameplayOrchestratorStats();

            registerGameplaySystemUseCase = new RegisterGameplaySystemUseCase(stats);
            registerIntentHandlersUseCase = new RegisterIntentHandlersUseCase(stats);
            handleGameplayIntentUseCase = new HandleGameplayIntentUseCase(stats);
        }

        public void RegisterSystem(IGameplaySystem system)
        {
            registerGameplaySystemUseCase.Execute(system);
        }

        public void RegisterIntentHandlers()
        {
            registerIntentHandlersUseCase.Execute();
        }

        public void HandleGameplayIntents(ulong tick, ICommittedIntentReader intents)
        {
            handleGameplayIntentUseCase.Execute(tick, intents);
        }

    }
}
