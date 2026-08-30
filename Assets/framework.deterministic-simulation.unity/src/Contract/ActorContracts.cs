using System;
using System.Collections.Generic;
using SimulationObjects.Contract;
using UnityEngine;

namespace DeterministicSimulation.Unity
{
    /// <summary>Pool-local instance identity. It is independent of a simulation object's ID or registry slot.</summary>
    public readonly struct InstanceHandle : IEquatable<InstanceHandle>
    {
        public InstanceHandle(int slot, uint generation)
        {
            if (slot < 0 || generation == 0) throw new ArgumentOutOfRangeException(nameof(slot));
            Slot = slot; Generation = generation;
        }
        public int Slot { get; }
        public uint Generation { get; }
        public bool IsValid => Generation != 0;
        public bool Equals(InstanceHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is InstanceHandle other && Equals(other);
        public override int GetHashCode() => unchecked(Slot * 397 ^ (int)Generation);
    }

    /// <summary>A committed, active logical object's presentation pose. No authoritative Unity state is read back.</summary>
    public readonly struct ActorPose
    {
        public ActorPose(SimulationObjectId id, int archetype, Vector3 position, Quaternion rotation)
        {
            if (!id.IsValid || archetype < 0) throw new ArgumentException("A pose needs a valid object ID and archetype.");
            float rotationLengthSquared = Quaternion.Dot(rotation, rotation);
            if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
                !Finite(rotationLengthSquared) || rotationLengthSquared < .000001f)
                throw new ArgumentException("A pose requires finite coordinates and a nonzero rotation.");
            Id = id; Archetype = archetype; Position = position; Rotation = rotation.normalized;
        }
        public SimulationObjectId Id { get; }
        public int Archetype { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IActorPoseSource
    {
        /// <summary>Returns active committed objects only. The adapter takes its own validated copy.</summary>
        IReadOnlyList<ActorPose> ReadPoses();
    }

    public readonly struct ActorBinding
    {
        internal ActorBinding(SimulationObjectId id, int archetype, InstanceHandle instance)
        { Id = id; Archetype = archetype; Instance = instance; }
        public SimulationObjectId Id { get; }
        public int Archetype { get; }
        public InstanceHandle Instance { get; }
    }
}
