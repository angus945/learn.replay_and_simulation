using System;
using System.Collections.Generic;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation.Framework;

namespace GameplayLessons
{
    internal static class Stage03Simulation
    {
        internal static void Run()
        {
            // This definition/world is linked from the existing guide example, not reimplemented here.
            MovementDefinitionExample definition = new MovementDefinitionExample();
            List<SimulationPhase> entered = new List<SimulationPhase>();
            using (SimulationSession<MovementWorld, float> session = definition.CreateSession(.25f,
                (SimulationPhase phase, bool entering) => { if (entering) entered.Add(phase); }, null))
            {
                session.EnqueueIntent(new PlayerMoveIntent(new CharacterId(1), MovementDirection.FromAxes(1f, 0f)));
                LessonAssert.Near(session.Observe(definition).X, 0f, "Enqueue is not execution");
                session.Step();
                LessonAssert.Near(session.Observe(definition).X, 1f, "The first fixed tick should move once");
                SimulationPhase[] expected = {
                    SimulationPhase.IntentAcquisition, SimulationPhase.IntentHandling,
                    SimulationPhase.PrePhysics, SimulationPhase.Physics, SimulationPhase.PostPhysics,
                    SimulationPhase.StructuralCommit, SimulationPhase.PresentationCapture
                };
                LessonAssert.That(entered.Count == expected.Length, "Each tick phase should be entered once");
                for (int index = 0; index < expected.Length; index++)
                    LessonAssert.That(entered[index] == expected[index], "Unexpected phase order at " + index);
                session.Step();
                LessonAssert.Near(session.Observe(definition).X, 2f, "Desired direction persists into tick two");
                session.Reset(.25f);
                LessonAssert.That(session.TickNumber == 0, "Reset rebuilds the clock");
                LessonAssert.Near(session.Observe(definition).X, 0f, "Reset rebuilds the same domain model");
            }
            LessonAssert.Throws<InvalidOperationException>(() => new MissingHandler().CreateSession(.25f),
                "Missing required handler must fail before a session starts");
            Console.WriteLine("  Definition wires intent -> application -> PrePhysics; two ticks yield X=2; Reset yields X=0.");
        }

        private sealed class MissingHandler : SimulationDefinition<MovementWorld, float>
        {
            protected override void ValidateScenario(float scenario) { }
            protected override float GetTickDelta(float scenario) => scenario;
            protected override MovementWorld CreateWorld(float scenario) => new MovementWorld();
            protected override void Configure(SimulationBuilder builder, MovementWorld world, float scenario)
                => builder.RequireIntent<PlayerMoveIntent>(); // Deliberately omit RegisterIntentHandler.
            protected override void DestroyWorld(MovementWorld world) { }
        }
    }
}
