using ECSManagement.API;
using ExternalIntent.Contract;

namespace ECSManagement.Contract
{
    public interface ISystemIntentHandler<TIntent> where TIntent : IExternalIntent
    {
        void HandleIntent(TIntent intent);
    }

    public interface ISystemIntentHandlerRegistry
    {
        void RegisterIntentHandler<TIntent>(ISystemIntentHandler<TIntent> handler) where TIntent : IExternalIntent;
    }

    public interface ISystem
    {
        void Initialize(IEcsWorld world);
        void RegisterIntentHandlers(ISystemIntentHandlerRegistry registry);
        void PrePhysicsTick(ulong tick, float deltaTime);
        void PostPhysicsTick(ulong tick, float deltaTime);
    }
}
