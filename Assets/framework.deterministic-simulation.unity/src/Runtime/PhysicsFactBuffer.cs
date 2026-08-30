using System;
using System.Collections.Generic;

namespace DeterministicSimulation.Unity
{
    /// <summary>Pure C# normalization: one fact per object pair/contact family/tick, with Enter taking precedence over Stay.</summary>
    internal sealed class PhysicsFactBuffer
    {
        private readonly int capacity;
        private readonly SortedSet<PhysicsFact> facts = new SortedSet<PhysicsFact>();

        internal PhysicsFactBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        internal void Add(PhysicsFact fact)
        {
            if (facts.Contains(fact)) return;
            switch (fact.Kind)
            {
                case PhysicsFactKind.TriggerEnter:
                    facts.Remove(new PhysicsFact(fact.First, fact.Second, PhysicsFactKind.TriggerStay));
                    break;
                case PhysicsFactKind.TriggerStay:
                    if (facts.Contains(new PhysicsFact(fact.First, fact.Second, PhysicsFactKind.TriggerEnter))) return;
                    break;
                case PhysicsFactKind.CollisionEnter:
                    facts.Remove(new PhysicsFact(fact.First, fact.Second, PhysicsFactKind.CollisionStay));
                    break;
                case PhysicsFactKind.CollisionStay:
                    if (facts.Contains(new PhysicsFact(fact.First, fact.Second, PhysicsFactKind.CollisionEnter))) return;
                    break;
                default:
                    throw new InvalidOperationException("The Unity fact buffer supports Enter/Stay only.");
            }
            // Apply phase precedence before checking capacity: an Enter upgrades, rather than adds to, a Stay.
            if (facts.Count >= capacity) throw new InvalidOperationException("Physics fact capacity exceeded.");
            facts.Add(fact);
        }

        internal IReadOnlyList<PhysicsFact> Capture() => new List<PhysicsFact>(facts).AsReadOnly();
        internal void Clear() => facts.Clear();
    }
}
