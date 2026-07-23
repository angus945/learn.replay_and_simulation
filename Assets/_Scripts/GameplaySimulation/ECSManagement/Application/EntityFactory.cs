using System;
using ECSManagement.Contract;
using ECSManagement.Domain;

namespace ECSManagement.Application
{
    internal sealed class EntityFactory
    {
        private readonly EntityRegistry entityRegistry;
        private readonly ComponentStores componentStores;

        public EntityFactory(EntityRegistry entityRegistry, ComponentStores componentStores)
        {
            this.entityRegistry = entityRegistry ??
                throw new ArgumentNullException(nameof(entityRegistry));
            this.componentStores = componentStores ??
                throw new ArgumentNullException(nameof(componentStores));
        }

        public EntityHandle Spawn<TArguments>(
            IEntityRecipe<TArguments> recipe,
            in TArguments arguments)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            EntityHandle entity = entityRegistry.Reserve();

            try
            {
                recipe.Build(componentStores, entity, in arguments);
                entityRegistry.CommitCreate(entity);
                return entity;
            }
            catch
            {
                componentStores.RemoveAllComponents(entity);
                entityRegistry.AbortCreate(entity);
                throw;
            }
        }
    }
}
