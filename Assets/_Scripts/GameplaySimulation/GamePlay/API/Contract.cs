using GamePlay.API;
using ExternalIntent.Contract;

namespace GamePlay.Contract
{
    public interface IIntentHandler<TIntent> where TIntent : IExternalIntent
    {
        void HandleIntent(TIntent intent);
    }

    public interface IGameplaySystem
    {
        void RegisterIntentHandler(IIntentHandlerRegistry registry);
        void PrePhysicsTick(float deltaTime);
        void PostPhysicsTick(float deltaTime);
    }
}
