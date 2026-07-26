using System;
using System.Collections.Generic;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Infrastructure
{
    public class ActorBindings
    {
        public int Count => sortedBindings.Count;
        SortedList<ulong, ActorBinding> sortedBindings = new SortedList<ulong, ActorBinding>();
        Dictionary<EntityHandle, ActorBinding> entityBindings = new Dictionary<EntityHandle, ActorBinding>();

        public ActorBinding Bind(EntityHandle entity, ActorHandle actorHandle)
        {
            ActorBinding binding = new ActorBinding(entity, actorHandle);
            entityBindings[entity] = binding;
            sortedBindings[entity.SequenceId] = binding;
            return binding;
        }
        public ActorBinding GetActiveBinding(int index)
        {
            if (index < 0 || index >= sortedBindings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range for active bindings.");
            }

            return sortedBindings.Values[index];
        }
        public bool HasBinding(EntityHandle entity)
        {
            return entityBindings.ContainsKey(entity);
        }

        public bool Contains(ActorBinding binding)
        {
            return entityBindings.TryGetValue(binding.Entity, out ActorBinding existingBinding) && existingBinding.Equals(binding);
        }

        public void Unbind(ActorBinding binding)
        {
            entityBindings.Remove(binding.Entity);
            sortedBindings.Remove(binding.Entity.SequenceId);
        }
    }
}
