using System;
using SimulationCore.ExternalCommands.PlayerInput.Domain;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.Port;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class ProduceInputCommandUseCase
    {
        readonly InputStats inputStats;
        readonly ICommandPort commandPort;
        readonly IRuleRegistrationPort rulePort;

        internal ProduceInputCommandUseCase(InputStats stats, ICommandPort commandPort, IRuleRegistrationPort rulePort)
        {
            this.inputStats = stats;
            this.commandPort = commandPort;
            this.rulePort = rulePort;
        }

        internal void Execute(ulong tick)
        {
            if (!inputStats.isInitialized)
                throw new InvalidOperationException("InputStats has not been initialized. Call Initialize() before producing input commands.");

            UpdateInputStats(tick);
            ProduceInputCommand(tick);
        }
        void UpdateInputStats(ulong tick)
        {
            TickInputFrame frame = inputStats.reusableFrame;
            frame.SetTick(tick);

            for (int i = 0; i < inputStats.buttonStateReader.Count; i++)
            {
                frame.Buttons[i] = inputStats.buttonStateReader[i].ConsumeTickInput();
            }

            for (int i = 0; i < inputStats.axisStateReader.Count; i++)
            {
                frame.Axes[i] = inputStats.axisStateReader[i].ReadTickInput();
            }
        }
        void ProduceInputCommand(ulong tick)
        {
            for (int i = 0; i < rulePort.RuleCount; i++)
            {
                IInputCommandRule rule = rulePort.GetCommandRule(i);
                if (rule.TryProduce(inputStats.snapshot, out ICommand command))
                {
                    commandPort.EnqueueCommand(CommandMetadata.External(tick, CommandSource.Input), command);
                }
            }
        }
    }
}
