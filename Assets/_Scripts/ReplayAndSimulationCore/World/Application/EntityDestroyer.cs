using System.Collections.Generic;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace SimulationCore.World.Application
{
    sealed class EntityDestroyer
    {
        Entities entities;
        ComponentStores components;

        readonly List<EntityHandle> pendingDestroyRequests = new();

        public EntityDestroyer(Entities entities, ComponentStores components)
        {
            this.entities = entities;
            this.components = components;
        }

        public void DestroyRequest(EntityHandle entity)
        {
            pendingDestroyRequests.Add(entity);
            entities.MarkForDestroy(entity.SlotId, entity.SequenceId);
        }
        public void CommitDestroyRequests()
        {
            for (int i = 0; i < pendingDestroyRequests.Count; i++)
            {
                EntityHandle entity = pendingDestroyRequests[i];
                components.RemoveAllComponents(entity.SlotId);
                entities.CommitDestroy(entity.SlotId, entity.SequenceId);
            }
            pendingDestroyRequests.Clear();
        }
    }
}
