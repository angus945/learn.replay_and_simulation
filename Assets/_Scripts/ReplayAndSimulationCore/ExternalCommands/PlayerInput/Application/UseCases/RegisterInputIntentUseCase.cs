using System;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.Contract;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class RegisterInputCommandUseCase
    {
        // readonly CommandStats stats;
        readonly IRuleRegistrationPort registrationPort;

        internal RegisterInputCommandUseCase(IRuleRegistrationPort registrationPort)
        {
            this.registrationPort = registrationPort ?? throw new ArgumentNullException(nameof(registrationPort));
        }

        internal void Execute<TCommand>(IInputCommandRule commandRule) where TCommand : struct, ICommand
        {
            if (commandRule == null)
                throw new ArgumentNullException(nameof(commandRule));

            if (registrationPort.IsInputCommandRuleRegistered<TCommand>())
            {
                throw new InvalidOperationException(
                    $"Input command rule for {typeof(TCommand).FullName} is already registered."
                );
            }

            registrationPort.RegisterInputCommandRule<TCommand>(commandRule);
        }
    }
}
