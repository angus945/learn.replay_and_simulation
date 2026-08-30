using UnityEngine;

namespace DeterministicSimulation.Unity
{
    [DisallowMultipleComponent]
    public sealed class ActorCollisionRelay : MonoBehaviour
    {
        private UnityActorBinding binding;
        internal void Initialize(UnityActorBinding owner) { binding = owner; }
        private void OnCollisionEnter(Collision collision) => Record(collision.collider, PhysicsFactKind.CollisionEnter);
        private void OnCollisionStay(Collision collision) => Record(collision.collider, PhysicsFactKind.CollisionStay);
        private void OnTriggerEnter(Collider other) => Record(other, PhysicsFactKind.TriggerEnter);
        private void OnTriggerStay(Collider other) => Record(other, PhysicsFactKind.TriggerStay);
        // Exit callbacks can arrive after collider reuse and contain no original binding generation.
        // This logical-authority adapter deliberately reports only Enter/Stay; removal comes from the pose snapshot.

        private void Record(Collider other, PhysicsFactKind kind)
        {
            if (binding == null || !binding.IsBound || binding.PhysicsOwner == null || other == null) return;
            UnityActorBinding otherBinding = other.GetComponentInParent<UnityActorBinding>();
            if (otherBinding == null || !otherBinding.IsBound || otherBinding.PhysicsOwner != binding.PhysicsOwner ||
                otherBinding.ObjectId == binding.ObjectId) return;
            binding.PhysicsOwner.Record(new PhysicsFact(binding.ObjectId, otherBinding.ObjectId, kind));
        }
    }
}
