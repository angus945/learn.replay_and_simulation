using System;
using GameplaySimulation;
using Testability;
using Testability.Templates;

namespace GameplayLessons
{
    internal static class Stage04Testability
    {
        internal static void Run()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f, includeEnemy: false,
                maxTicks: 4, maxActions: 4);
            GameplayDefinition definition = new GameplayDefinition();
            using (TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session =
                definition.CreateTestSession(scenario))
            {
                ITemplateGameplay<GameplayInput, GameplayObservation> gameplay = session.Gameplay;
                GameplayObservation initial = gameplay.Observe();
                ulong player = initial.PlayerId;
                LessonAssert.That(session.Limits.MaxTicks == 4 && session.Limits.MaxInputs == 4,
                    "The game definition must map its scenario budget");

                // Submit sequence 2 before 1: target-tick execution still follows sequence order.
                SubmissionResult second = gameplay.Submit(session.Id, 2, 2, new GameplayInput(GameplayActionKind.Move, player, x: -1f));
                SubmissionResult first = gameplay.Submit(session.Id, 1, 2, new GameplayInput(GameplayActionKind.Move, player, x: 1f));
                SubmissionResult unknown = gameplay.Submit(session.Id, 3, 2, new GameplayInput(GameplayActionKind.Move, 99, x: 1f));
                LessonAssert.That(first.Queued && second.Queued && unknown.Queued,
                    "Admission accepts valid envelopes, including an actor that gameplay may reject");
                LessonAssert.That(!gameplay.Submit(session.Id, 1, 2, new GameplayInput(GameplayActionKind.Move, player)).Queued,
                    "Duplicate sequence must be rejected at admission");
                LessonAssert.That(session.Simulation.Step().Results.Count == 0, "Tick one has no due inputs");
                LessonAssert.Near(GameplayLessonState.Player(gameplay.Observe()).X, 0f, "Input must wait for tick two");

                TemplateTick tick = session.Simulation.Step();
                LessonAssert.That(tick.Results.Count == 3 && tick.Results[0].Sequence == 1 && tick.Results[1].Sequence == 2,
                    "Execution uses sequence order, not arrival order");
                LessonAssert.That(tick.Results[0].Status == ActionStatus.Accepted && tick.Results[1].Status == ActionStatus.Accepted,
                    "The existing player accepts both direction changes");
                LessonAssert.That(tick.Results[2].Status == ActionStatus.Rejected && tick.Results[2].Code == "actor.unknown",
                    "Unknown actor is a gameplay rejection, not a crash");
                LessonAssert.Near(GameplayLessonState.Player(gameplay.Observe()).X, -1f, "Last ordered direction wins before movement");
                LessonAssert.Near(GameplayLessonState.Player(initial).X, 0f, "An earlier snapshot must remain unchanged");
                LessonAssert.That(session.Results.Find(session.Id, 3).State == "Completed", "Results can be queried after execution");

                string oldId = session.Id;
                session.Admin.Reset(scenario);
                LessonAssert.That(session.Id != oldId && session.CurrentTick == 0, "Reset creates a new session identity");
                LessonAssert.That(gameplay.Submit(oldId, 4, 1, new GameplayInput(GameplayActionKind.Move, player)).Code == "session.stale",
                    "The previous identity cannot control the rebuilt world");
            }
            Console.WriteLine("  Queued is not Accepted; tick 2 orders 1,2,3; unknown actor is rejected; Reset rejects old identity.");
        }
    }
}
