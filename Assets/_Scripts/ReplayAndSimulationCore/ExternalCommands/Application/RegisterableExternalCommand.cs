using System.Collections.Generic;
using SimulationCore.CommandSystem.API;
using SimulationCore.ExternalCommands.API;
using SimulationCore.ExternalCommands.Contract;

namespace SimulationCore.ExternalCommands
{
    public class RegisterableExternalCommand : ISimulationExternalCommands
    {
        List<IExternalCommandProvider> externalCommandProviders = new List<IExternalCommandProvider>();

        public void RegisterExternalCommandProvider(IExternalCommandProvider provider)
        {
            if (externalCommandProviders.Contains(provider))
            {
                throw new System.Exception($"Provider {provider.GetType().Name} is already registered.");
            }

            externalCommandProviders.Add(provider);
        }

        public void AcquireCommands(ulong tick, float delta)
        {
            AcquireCommands(tick);
        }

        public void AcquireCommands(ulong tick)
        {
            foreach (var provider in externalCommandProviders)
            {
                provider.EnqueueCommands(tick);
            }
        }
    }


}
