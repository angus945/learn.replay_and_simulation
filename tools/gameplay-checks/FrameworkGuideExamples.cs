using System;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation.Framework;
using GameplaySimulation;
using Testability;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

internal static class FrameworkGuideExamples
{
    internal static void Run()
    {
        MinimalMovement();
        ControlledMovement();
        Console.WriteLine("PASS: framework guide examples (minimal composition and controlled movement).");
    }

    private static void MinimalMovement()
    {
        CharacterId id = new CharacterId(1);
        MovementAggregate actor = new MovementAggregate(id, new MovementPosition(0, 0), 4);
        CharacterMovementRepository repository = new CharacterMovementRepository();
        repository.Add(actor);
        MovementApplication application = new MovementApplication(repository);
        SimulationPipeline pipeline = new SimulationPipeline();
        pipeline.RegisterIntentHandler(new PlayerMoveIntentHandler(application));
        pipeline.RegisterPrePhysicsParticipant(new MovementPrePhysicsParticipant(application));
        pipeline.Seal();
        SimulationRunner runner = new SimulationRunner(pipeline, .25f);

        pipeline.EnqueueIntent(new PlayerMoveIntent(id, MovementDirection.FromAxes(1, 0)));
        Require(actor.Position.X == 0, "Enqueue must not move the actor.");
        runner.AdvanceTick();
        Require(actor.Position.X == 1 && runner.TickNumber == 1, "First movement tick.");
        runner.AdvanceTick();
        Require(actor.Position.X == 2, "Desired direction persists between ticks.");
    }

    private static void ControlledMovement()
    {
        GameplaySession session = new GameplaySession();
        session.Admin.Start(new GameplayScenario(tickDelta: .25f, includeEnemy: false));
        SubmissionResult admission = session.Gameplay.Submit(new GameplayRequest(
            session.Id, 1, 2, GameplayActionKind.Move, 1, x: 1));
        Require(admission.Queued && session.Gameplay.Observe().Actors[0].X == 0, "Admission is not execution.");
        TickReport first = session.Simulation.Step();
        Require(first.Results.Count == 0 && session.Gameplay.Observe().Actors[0].X == 0, "Target tick must be respected.");
        TickReport second = session.Simulation.Step();
        Require(second.Results.Count == 1 && second.Results[0].Status == ActionStatus.Accepted, "Execution result.");
        Require(session.Gameplay.Observe().Actors[0].X == 1, "Controlled movement position.");
        Require(session.Results.Find(session.Id, 1).State == ActionLookupState.Completed, "Result query.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Guide example: " + message);
    }
}
