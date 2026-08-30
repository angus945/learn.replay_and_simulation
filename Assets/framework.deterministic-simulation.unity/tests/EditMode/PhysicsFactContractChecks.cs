using System;
using System.Collections.Generic;
using SimulationObjects.Contract;

namespace DeterministicSimulation.Unity.Tests
{
    /// <summary>Runs without Unity native calls or NUnit; also wrapped by the EditMode suite.</summary>
    public static class PhysicsFactContractChecks
    {
        public static void PhasePrecedenceIsIndependentOfCallbackOrder()
        {
            PhysicsFact enter = Fact(2, 9, PhysicsFactKind.TriggerEnter);
            PhysicsFact stay = Fact(9, 2, PhysicsFactKind.TriggerStay);
            PhysicsFactBuffer stayFirst = new PhysicsFactBuffer(1);
            stayFirst.Add(stay); stayFirst.Add(enter); stayFirst.Add(stay);
            PhysicsFactBuffer enterFirst = new PhysicsFactBuffer(1);
            enterFirst.Add(enter); enterFirst.Add(stay); enterFirst.Add(enter);
            IReadOnlyList<PhysicsFact> left = stayFirst.Capture();
            IReadOnlyList<PhysicsFact> right = enterFirst.Capture();
            Check(left.Count == 1 && right.Count == 1, "Enter plus Stay must count as one logical contact.");
            Check(left[0].CompareTo(right[0]) == 0 && left[0].Kind == PhysicsFactKind.TriggerEnter,
                "Enter must win in either callback order.");

            PhysicsFactBuffer families = new PhysicsFactBuffer(2);
            families.Add(stay);
            families.Add(Fact(2, 9, PhysicsFactKind.CollisionStay));
            families.Add(Fact(9, 2, PhysicsFactKind.CollisionEnter));
            families.Add(enter);
            IReadOnlyList<PhysicsFact> separated = families.Capture();
            Check(separated.Count == 2 && separated[0].Kind == PhysicsFactKind.CollisionEnter && separated[1].Kind == PhysicsFactKind.TriggerEnter,
                "Collision and trigger families remain distinct and stably ordered.");
        }

        public static void CapacityCountsNormalizedContactsAndSnapshotsAreDetached()
        {
            PhysicsFactBuffer buffer = new PhysicsFactBuffer(1);
            buffer.Add(Fact(2, 9, PhysicsFactKind.TriggerStay));
            buffer.Add(Fact(2, 9, PhysicsFactKind.TriggerEnter));
            IReadOnlyList<PhysicsFact> saved = buffer.Capture();
            bool overflow = false;
            try { buffer.Add(Fact(3, 9, PhysicsFactKind.TriggerEnter)); }
            catch (InvalidOperationException) { overflow = true; }
            Check(overflow && buffer.Capture().Count == 1, "A distinct contact must still exceed capacity without replacing existing evidence.");
            buffer.Clear();
            buffer.Add(Fact(3, 9, PhysicsFactKind.TriggerStay));
            Check(saved.Count == 1 && saved[0].First.Value == 2 && saved[0].Kind == PhysicsFactKind.TriggerEnter,
                "A later tick must not mutate the previously captured fact batch.");
            Check(buffer.Capture()[0].First.Value == 3, "Clearing a tick must release capacity.");
        }

        private static PhysicsFact Fact(ulong first, ulong second, PhysicsFactKind kind)
            => new PhysicsFact(new SimulationObjectId(first), new SimulationObjectId(second), kind);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
