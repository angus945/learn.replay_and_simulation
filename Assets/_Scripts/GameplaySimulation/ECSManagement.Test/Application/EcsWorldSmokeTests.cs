using ECSManagement.Application;
using ECSManagement.Contract;
using ECSManagement.Domain;
using ECSManagement.Test.Fixtures;
using NUnit.Framework;

namespace ECSManagement.Test.Application
{
    public sealed class EcsWorldSmokeTests
    {
        [Test]
        public void RuntimeAssemblyCanBeReferencedFromEditModeTests()
        {
            EcsWorld world = new EcsWorld(2);
            world.RegisterComponent<TestComponent>();

            EntityHandle entity = world.Spawn(
                new TestEntityRecipe(),
                new TestSpawnArguments(42));

            Assert.That(world.HasComponent<TestComponent>(entity), Is.True);
            Assert.That(world.GetComponent<TestComponent>(entity).Value, Is.EqualTo(42));
        }

        [Test]
        public void RuntimeInternalsAreVisibleToEditModeTests()
        {
            EntityRegistry registry = new EntityRegistry(1);

            EntityHandle entity = registry.Reserve();
            registry.CommitCreate(entity);

            Assert.That(registry.Capacity, Is.EqualTo(1));
            Assert.That(registry.IsAlive(entity), Is.True);
        }

    }
}
