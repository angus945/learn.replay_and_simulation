using System;
using GamePlay.Contract;

namespace GamePlay.Application
{
    internal sealed class RegisterGameplaySystemUseCase
    {
        readonly GameplayOrchestratorStats stats;

        internal RegisterGameplaySystemUseCase(GameplayOrchestratorStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(IGameplaySystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            stats.Systems.Add(system);
        }
    }
}
