using System;
using TickCommandSystem.Contract;
using TickIntentsBuilder.Contract;

namespace TickIntentsBuilder.Application
{
    internal sealed class RegisterInputCommandUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal RegisterInputCommandUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute<TCommand>(
            IInputCommandRule commandRule)
            where TCommand : struct, ICommand
        {
            if (commandRule == null)
                throw new ArgumentNullException(nameof(commandRule));

            Type commandType = typeof(TCommand);
            if (stats.InputCommandRuleIndexByType.ContainsKey(commandType))
            {
                throw new InvalidOperationException(
                    $"Input command rule for {commandType.FullName} is already registered."
                );
            }

            int index = stats.InputCommandRules.Count;
            stats.InputCommandRuleIndexByType.Add(commandType, index);
            stats.InputCommandRules.Add(commandRule);
        }
    }
}
