namespace SimulationCore.World.Contract
{
    public interface IEntityArguments
    {
    }

    public interface IEntityBuildContext
    {
        void AddComponent<T>(T component) where T : IComponent;
    }

    public interface IEntityRecipe<TArguments>
    {
        void Build(IEntityBuildContext context, in TArguments arguments);
    }
}
