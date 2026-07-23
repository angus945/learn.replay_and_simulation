using ECSManagement.Contract;

namespace ECSManagement.Test.Fixtures
{
    internal readonly struct TestComponent : IComponent
    {
        public readonly int Value;

        public TestComponent(int value)
        {
            Value = value;
        }
    }

    internal readonly struct SecondaryTestComponent : IComponent
    {
        public readonly int Value;

        public SecondaryTestComponent(int value)
        {
            Value = value;
        }
    }

    internal readonly struct ExcludedTestComponent : IComponent
    {
    }

    internal readonly struct TestSpawnArguments : IEntityArguments
    {
        public readonly int ComponentValue;

        public TestSpawnArguments(int componentValue)
        {
            ComponentValue = componentValue;
        }
    }

    internal readonly struct EmptySpawnArguments : IEntityArguments
    {
    }

    internal sealed class TestEntityRecipe : IEntityRecipe<TestSpawnArguments>
    {
        public void Build(
            IEntityBuildContext context,
            EntityHandle entity,
            in TestSpawnArguments arguments)
        {
            context.AddComponent(
                entity,
                new TestComponent(arguments.ComponentValue));
        }
    }

    internal sealed class EmptyEntityRecipe : IEntityRecipe<EmptySpawnArguments>
    {
        public void Build(
            IEntityBuildContext context,
            EntityHandle entity,
            in EmptySpawnArguments arguments)
        {
        }
    }
}
