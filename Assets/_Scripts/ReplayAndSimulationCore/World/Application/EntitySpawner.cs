using System;
using System.Collections.Generic;
using SimulationCore.World.Contract;

namespace SimulationCore.World.Application
{
    sealed class EntitySpawner
    {
        private EntityFactory entityFactory;

        private readonly List<ISpawnRequest> pendingSpawnRequests = new();

        public EntitySpawner(EntityFactory entityFactory)
        {
            this.entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        }
        internal void SpawnRequest<TArgs>(IEntityRecipe<TArgs> spawnPlayerRecipe, TArgs spawnPlayerArguments) where TArgs : IEntityArguments
        {
            pendingSpawnRequests.Add(new PendingSpawnRequest<TArgs>(spawnPlayerRecipe, spawnPlayerArguments));
        }
        public void CommitSpawnRequests()
        {
            for (int i = 0; i < pendingSpawnRequests.Count; i++)
            {
                pendingSpawnRequests[i].Commit(entityFactory);
            }
            pendingSpawnRequests.Clear();
        }

        private interface ISpawnRequest
        {
            EntityHandle Commit(EntityFactory factory);
        }
        private sealed class PendingSpawnRequest<TArguments> : ISpawnRequest
        {
            private readonly IEntityRecipe<TArguments> recipe;
            private readonly TArguments arguments;

            public PendingSpawnRequest(IEntityRecipe<TArguments> recipe, in TArguments arguments)
            {
                this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
                this.arguments = arguments;
            }

            public EntityHandle Commit(EntityFactory factory)
            {
                return factory.Spawn(recipe, in arguments);
            }
        }
    }
}
