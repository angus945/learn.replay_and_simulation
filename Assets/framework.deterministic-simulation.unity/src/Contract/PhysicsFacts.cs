using System;
using System.Collections.Generic;
using SimulationObjects.Contract;

namespace DeterministicSimulation.Unity
{
    /// <summary>Contact data kinds. The Unity relay emits Enter/Stay only; Exit requires a source with reliable lifetime identity.</summary>
    public enum PhysicsFactKind { CollisionEnter, CollisionStay, CollisionExit, TriggerEnter, TriggerStay, TriggerExit }

    /// <summary>Canonical unordered object pair plus contact phase. Multiple colliders and mirrored callbacks collapse to one fact.</summary>
    public readonly struct PhysicsFact : IComparable<PhysicsFact>
    {
        public PhysicsFact(SimulationObjectId first, SimulationObjectId second, PhysicsFactKind kind)
        {
            if (!first.IsValid || !second.IsValid || first == second || !Enum.IsDefined(typeof(PhysicsFactKind), kind))
                throw new ArgumentException("A contact needs two distinct valid object IDs and a supported kind.");
            First = first.CompareTo(second) < 0 ? first : second;
            Second = first.CompareTo(second) < 0 ? second : first;
            Kind = kind;
        }
        public SimulationObjectId First { get; }
        public SimulationObjectId Second { get; }
        public PhysicsFactKind Kind { get; }
        public int CompareTo(PhysicsFact other)
        {
            int order = First.CompareTo(other.First);
            if (order != 0) return order;
            order = Second.CompareTo(other.Second);
            return order != 0 ? order : Kind.CompareTo(other.Kind);
        }
    }

    public interface IPhysicsFactSink
    {
        /// <summary>Called once after simulation, never inside a Unity collision callback. Implement gameplay mapping outside this assembly.</summary>
        void PublishPhysicsFacts(ulong tick, IReadOnlyList<PhysicsFact> facts);
    }
}
