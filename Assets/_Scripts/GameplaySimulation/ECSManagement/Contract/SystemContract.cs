using ECSManagement.API;

namespace ECSManagement.Contract
{
    public interface ISystem
    {
        void Initialize(IEcsWorld world);
        void PrePhysicsTick(ulong tick, float deltaTime);
        void PostPhysicsTick(ulong tick, float deltaTime);
    }
}
