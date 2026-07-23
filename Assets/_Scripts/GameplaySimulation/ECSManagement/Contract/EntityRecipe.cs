namespace ECSManagement.Contract
{
    public interface IEntityArguments
    {
    }

    public interface IEntityBuildContext
    {
        void AddComponent<T>(EntityHandle entity, T component) where T : IComponent;
    }

    public interface IEntityRecipe<TArguments>
    {
        void Build(IEntityBuildContext context, EntityHandle entity, in TArguments arguments);
    }
}
