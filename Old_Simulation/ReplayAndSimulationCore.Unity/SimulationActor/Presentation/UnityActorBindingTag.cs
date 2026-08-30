using SimulationCore.SimulationActor.Application.Dto;
using UnityEngine;

namespace SimulationCore.SimulationActor.Presentation
{
    public interface IActorBindingTag
    {
        ActorBinding GetBinding();
    }
    public class UnityActorBindingTag : MonoBehaviour, IActorBindingTag
    {
        ActorBinding binding;
        public void SetBinding(ActorBinding binding)
        {
            this.binding = binding;
        }
        public ActorBinding GetBinding()
        {
            return binding;

        }
        public void Unbind()
        {
            binding = default;
        }
    }
}
