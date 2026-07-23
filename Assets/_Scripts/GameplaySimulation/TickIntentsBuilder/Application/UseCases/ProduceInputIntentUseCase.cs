using System;
using SimulationInput.API;
using TickCommandSystem.Contract;
using TickIntentsBuilder.Contract;

namespace TickIntentsBuilder.Application
{
    internal sealed class ProduceInputCommandsUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal ProduceInputCommandsUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(IInputSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            stats.CommandBuffer.BeginProduce();

            for (int i = 0; i < stats.InputCommandRules.Count; i++)
            {
                IInputCommandRule rule = stats.InputCommandRules[i];
                if (rule.TryProduce(snapshot, out ICommand command))
                {
                    stats.CommandBuffer.Submit(command);
                }
            }
        }
    }
}
