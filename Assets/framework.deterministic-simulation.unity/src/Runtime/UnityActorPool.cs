using System;
using System.Collections.Generic;
using SimulationObjects.Contract;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeterministicSimulation.Unity
{
    /// <summary>Bounded prefab pool with deterministic allocation. This adapter owns all instances it creates.</summary>
    public sealed class UnityActorPool : IDisposable
    {
        private sealed class PrefabPool
        {
            internal GameObject Prefab;
            internal int Capacity;
            internal readonly SortedSet<int> Free = new SortedSet<int>();
        }
        private sealed class Slot
        {
            internal int Archetype;
            internal uint Generation = 1;
            internal GameObject Instance;
            internal UnityActorBinding Binding;
        }
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly SortedDictionary<int, PrefabPool> prefabs = new SortedDictionary<int, PrefabPool>();
        private readonly SortedDictionary<SimulationObjectId, int> active = new SortedDictionary<SimulationObjectId, int>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly GameObject root;
        private bool disposed;
        private bool mutating;
        private LocalPhysicsParticipant physicsOwner;

        public UnityActorPool(string name = "Simulation Actor Instances")
        {
            root = new GameObject(name);
            root.SetActive(false);
        }
        public bool IsSealed { get; private set; }
        public int ActiveCount => active.Count;

        public void RegisterPrefab(int archetype, GameObject prefab, int capacity)
        {
            EnsureAlive();
            EnsureNotMutating();
            if (IsSealed) throw new InvalidOperationException("Prefab registration is sealed.");
            if (archetype < 0 || capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (prefabs.ContainsKey(archetype)) throw new InvalidOperationException("Duplicate prefab archetype.");
            prefabs.Add(archetype, new PrefabPool { Prefab = prefab, Capacity = capacity });
        }

        public void Seal()
        {
            EnsureAlive();
            EnsureNotMutating();
            if (IsSealed) return;
            mutating = true;
            try
            {
                foreach (KeyValuePair<int, PrefabPool> entry in prefabs)
                {
                    for (int index = 0; index < entry.Value.Capacity; index++)
                    {
                        GameObject instance = UnityEngine.Object.Instantiate(entry.Value.Prefab, root.transform);
                        instance.name = entry.Key + " instance " + index;
                        instance.SetActive(false);
                        UnityActorBinding binding = instance.GetComponent<UnityActorBinding>();
                        if (binding == null) binding = instance.AddComponent<UnityActorBinding>();
                        binding.Unbind();
                        entry.Value.Free.Add(slots.Count);
                        slots.Add(new Slot { Archetype = entry.Key, Instance = instance, Binding = binding });
                    }
                }
                IsSealed = true;
                root.SetActive(true);
            }
            catch { DisposeCore(); throw; }
            finally { mutating = false; }
        }

        /// <summary>Reconciles a complete committed snapshot in ID order. Missing objects release their instances first.</summary>
        public void Reconcile(IReadOnlyList<ActorPose> poses)
        {
            EnsureSealed();
            EnsureNotMutating();
            mutating = true;
            try { ReconcileCore(poses); }
            finally { mutating = false; }
        }

        private void ReconcileCore(IReadOnlyList<ActorPose> poses)
        {
            SortedDictionary<SimulationObjectId, ActorPose> desired = CopyPoses(poses);
            Dictionary<int, int> required = new Dictionary<int, int>();
            foreach (ActorPose pose in desired.Values)
            {
                if (!prefabs.TryGetValue(pose.Archetype, out PrefabPool prefab))
                    throw new InvalidOperationException("No prefab registered for archetype " + pose.Archetype);
                required.TryGetValue(pose.Archetype, out int count);
                if (++count > prefab.Capacity) throw new InvalidOperationException("Actor pool capacity exceeded for archetype " + pose.Archetype);
                required[pose.Archetype] = count;
                if (active.TryGetValue(pose.Id, out int oldSlot) && slots[oldSlot].Archetype != pose.Archetype)
                    throw new InvalidOperationException("An active object's archetype cannot change.");
            }
            List<SimulationObjectId> removed = new List<SimulationObjectId>();
            foreach (SimulationObjectId id in active.Keys)
                if (!desired.ContainsKey(id)) removed.Add(id);
            foreach (SimulationObjectId id in removed) Release(id);
            foreach (ActorPose pose in desired.Values)
            {
                if (!active.TryGetValue(pose.Id, out int slotIndex))
                {
                    PrefabPool prefab = prefabs[pose.Archetype];
                    if (prefab.Free.Count == 0) throw new InvalidOperationException("Instance generations exhausted for this pool.");
                    slotIndex = prefab.Free.Min;
                    prefab.Free.Remove(slotIndex);
                    Slot allocated = slots[slotIndex];
                    allocated.Instance.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    allocated.Binding.Bind(pose.Id, new InstanceHandle(slotIndex, allocated.Generation));
                    active.Add(pose.Id, slotIndex);
                    allocated.Instance.SetActive(true);
                }
                slots[slotIndex].Instance.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            }
        }

        public IReadOnlyList<ActorBinding> GetActiveBindings()
        {
            EnsureSealed();
            List<ActorBinding> copy = new List<ActorBinding>();
            foreach (KeyValuePair<SimulationObjectId, int> entry in active)
            {
                Slot slot = slots[entry.Value];
                copy.Add(new ActorBinding(entry.Key, slot.Archetype, new InstanceHandle(entry.Value, slot.Generation)));
            }
            return copy.AsReadOnly();
        }

        public bool TryGetInstance(InstanceHandle handle, out GameObject instance)
        {
            EnsureSealed();
            instance = null;
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= slots.Count) return false;
            Slot slot = slots[handle.Slot];
            if (slot.Generation != handle.Generation || !slot.Binding.IsBound) return false;
            instance = slot.Instance;
            return instance != null;
        }

        internal void AttachPhysics(LocalPhysicsParticipant owner, Scene scene)
        {
            EnsureSealed();
            EnsureNotMutating();
            if (physicsOwner != null || active.Count != 0) throw new InvalidOperationException("Attach a physics owner before binding actors.");
            foreach (Slot slot in slots)
            {
                foreach (Rigidbody body in slot.Instance.GetComponentsInChildren<Rigidbody>(true))
                    if (!body.isKinematic) throw new InvalidOperationException("Logical-authority proxies require kinematic rigidbodies.");
            }
            SceneManager.MoveGameObjectToScene(root, scene);
            physicsOwner = owner;
            foreach (Slot slot in slots)
            {
                slot.Binding.PhysicsOwner = owner;
                foreach (Collider collider in slot.Instance.GetComponentsInChildren<Collider>(true))
                    AddRelay(collider.gameObject, slot.Binding);
                foreach (Rigidbody body in slot.Instance.GetComponentsInChildren<Rigidbody>(true))
                    AddRelay(body.gameObject, slot.Binding);
            }
        }

        private static void AddRelay(GameObject target, UnityActorBinding binding)
        {
            ActorCollisionRelay relay = target.GetComponent<ActorCollisionRelay>();
            if (relay == null) relay = target.AddComponent<ActorCollisionRelay>();
            relay.Initialize(binding);
        }

        internal static SortedDictionary<SimulationObjectId, ActorPose> CopyPoses(IReadOnlyList<ActorPose> poses)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            SortedDictionary<SimulationObjectId, ActorPose> copy = new SortedDictionary<SimulationObjectId, ActorPose>();
            foreach (ActorPose pose in poses)
            {
                ActorPose validated = new ActorPose(pose.Id, pose.Archetype, pose.Position, pose.Rotation);
                if (copy.ContainsKey(validated.Id)) throw new ArgumentException("Duplicate object pose.");
                copy.Add(validated.Id, validated);
            }
            return copy;
        }

        private void Release(SimulationObjectId id)
        {
            int index = active[id];
            Slot slot = slots[index];
            slot.Binding.Unbind();
            slot.Instance.SetActive(false);
            active.Remove(id);
            if (slot.Generation == uint.MaxValue) return;
            slot.Generation++;
            prefabs[slot.Archetype].Free.Add(index);
        }

        public void Dispose()
        {
            EnsureOwnerThread();
            if (disposed) return;
            EnsureNotMutating();
            DisposeCore();
        }

        private void DisposeCore()
        {
            disposed = true;
            foreach (Slot slot in slots)
            {
                if (slot.Binding != null) { slot.Binding.Unbind(); slot.Binding.PhysicsOwner = null; }
            }
            active.Clear();
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }

        private void EnsureSealed()
        {
            EnsureAlive();
            if (!IsSealed) throw new InvalidOperationException("Seal prefab registration first.");
        }
        private void EnsureAlive()
        {
            EnsureOwnerThread();
            if (disposed) throw new ObjectDisposedException(nameof(UnityActorPool));
        }
        private void EnsureOwnerThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Use the actor pool owner thread.");
        }
        private void EnsureNotMutating()
        {
            if (mutating) throw new InvalidOperationException("Actor lifecycle callbacks cannot reenter or dispose the pool.");
        }
    }
}
