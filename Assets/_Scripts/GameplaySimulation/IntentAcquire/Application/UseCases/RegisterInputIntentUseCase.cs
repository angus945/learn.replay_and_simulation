using System;
using ExternalIntent.Contract;

namespace ExternalIntent.Application
{
    internal sealed class RegisterInputIntentUseCase
    {
        readonly IntentAcquirerStats stats;

        internal RegisterInputIntentUseCase(IntentAcquirerStats stats)
        {
            this.stats = stats;
        }

        internal void Execute<TIntent>(IInputIntentRule intentRule) where TIntent : struct, IExternalIntent
        {
            if (intentRule == null)
                throw new ArgumentNullException(nameof(intentRule));

            Type intentType = typeof(TIntent);
            if (stats.InputIntentRuleIndexByType.ContainsKey(intentType))
            {
                throw new InvalidOperationException(
                    $"Input intent rule for {intentType.FullName} is already registered."
                );
            }

            int index = stats.InputIntentRules.Count;
            stats.InputIntentRuleIndexByType.Add(intentType, index);
            stats.InputIntentRules.Add(intentRule);
        }
    }
}
