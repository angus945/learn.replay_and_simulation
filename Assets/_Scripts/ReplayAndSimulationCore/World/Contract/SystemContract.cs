using SimulationCore.CommandSystem.API;
using SimulationCore.World.API;
using SimulationCore.World.Application;

namespace SimulationCore.World.Contract
{
    public interface ISystem
    {
        void Initialize(IEcsWorld world, ICommandHandleRegistryPort commandSubscriber);
    }
    public interface IPrePhysicsTick
    {
        void PrePhysicsTick(ulong tick, float deltaTime);
    }
    public interface IPostPhysicsTick
    {
        void PostPhysicsTick(ulong tick, float deltaTime);
    }
}
