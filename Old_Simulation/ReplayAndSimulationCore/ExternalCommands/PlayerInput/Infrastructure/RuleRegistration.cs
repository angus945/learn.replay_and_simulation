using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.Infrastructure
{
    public class RuleRegistration : IRuleRegistrationPort
    {
        internal readonly Dictionary<Type, int> InputCommandRuleIndexByType = new();
        internal readonly List<IInputCommandRule> InputCommandRules = new();

        public int RuleCount => InputCommandRules.Count;
        public IInputCommandRule GetCommandRule(int index)
        {
            if (index < 0 || index >= InputCommandRules.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {InputCommandRules.Count - 1}.");

            return InputCommandRules[index];
        }

        public bool IsInputCommandRuleRegistered<TCommand>() where TCommand : struct, ICommand
        {
            Type commandType = typeof(TCommand);
            return InputCommandRuleIndexByType.ContainsKey(commandType);
        }

        public void RegisterInputCommandRule<TCommand>(IInputCommandRule commandRule) where TCommand : struct, ICommand
        {
            Type commandType = typeof(TCommand);

            int index = InputCommandRules.Count;
            InputCommandRuleIndexByType.Add(commandType, index);
            InputCommandRules.Add(commandRule);
        }
    }
}
