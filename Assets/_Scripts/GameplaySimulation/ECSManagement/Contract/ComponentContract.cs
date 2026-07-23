namespace ECSManagement.Contract
{
    public interface IComponent
    {
    }

    public readonly struct ComponentEntry<T> where T : IComponent
    {
        public readonly EntityHandle Entity;
        public readonly T Component;

        public ComponentEntry(EntityHandle entity, T component)
        {
            Entity = entity;
            Component = component;
        }
    }
}
