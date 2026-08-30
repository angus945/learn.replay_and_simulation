using SimulationObjects.Contract;
using UnityEngine;

namespace DeterministicSimulation.Unity
{
    [DisallowMultipleComponent]
    public sealed class UnityActorBinding : MonoBehaviour
    {
        public SimulationObjectId ObjectId { get; private set; }
        public InstanceHandle Instance { get; private set; }
        public bool IsBound => ObjectId.IsValid;
        internal LocalPhysicsParticipant PhysicsOwner { get; set; }
        internal void Bind(SimulationObjectId id, InstanceHandle instance)
        { ObjectId = id; Instance = instance; }
        internal void Unbind() { ObjectId = default; Instance = default; }
    }
}
