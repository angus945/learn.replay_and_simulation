using System;
using ExternalIntent.Contract;
using SimulationInput.API;

namespace ExternalIntent.Application
{
    internal sealed class ProduceInputIntentUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal ProduceInputIntentUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(IInputSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            stats.IntentBuffer.BeginProduce();

            for (int i = 0; i < stats.InputIntentRules.Count; i++)
            {
                IInputIntentRule rule = stats.InputIntentRules[i];
                if (rule.TryProduce(snapshot, out IExternalIntent intent))
                {
                    stats.IntentBuffer.Submit(intent);
                }
            }
        }
    }
}
