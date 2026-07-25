using SimulationCore.CommandSystem.API;
using SimulationCore.World.API;
using SimulationCore.World.Application;

namespace SimulationCore.World.Contract
{
    public interface ISystem
    {
        void Initialize(IEcsWorld world, ICommandHandleRegistryPort commandSubscriber);
    }
    public interface ISystemPrePhysicsTick
    {
        void PrePhysicsTick(ulong tick, float deltaTime);
    }
    public interface ISystemPostPhysicsTick
    {
        void PostPhysicsTick(ulong tick, float deltaTime);
    }
}
