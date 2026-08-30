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
        MovementDefinitionExample.Verify();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.RealtimeTimingAndOwnership();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.RealtimeFailuresAndReentry();
        Testability.Tests.TemplateContractChecks.RealtimeRecordingAndOwnership();
        Console.WriteLine("PASS: composable realtime runner (timing, authority, failures and recording/replay).");
        MinimalWiringExample.Example.Run();
        Console.WriteLine("PASS: minimal wiring example (queue, movement, stop and reset).");
        GameplaySimulation.Tests.DemoTemplateChecks.Verify();
        Console.WriteLine("PASS: migrated demo template, legacy gameplay parity and replay frame matrix.");
        Testability.Tests.TemplateContractChecks.AdmissionAndDiagnostics();
        Testability.Tests.TemplateContractChecks.OrderingResetAndLimits();
        Testability.Tests.TemplateContractChecks.ReplayFrameMatrix();
        Testability.Tests.TemplateContractChecks.FailureReplay();
        Testability.Tests.TemplateContractChecks.InvariantAndCaptureFailures();
        Testability.Tests.TemplateContractChecks.DivergenceAndMalformedRecording();
        Testability.Tests.TemplateContractChecks.ThreadAndReentry();
        Testability.Tests.TemplateContractChecks.PhaseAndFileBounds();
        Console.WriteLine("PASS: testability/replay template contract checks (8 groups).");
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.Lifecycle();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.MissingConfiguration();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.FaultAndReentry();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.ResetFailures();
        DeterministicSimulation.Framework.Tests.SessionTemplateContractChecks.IndependentSessions();
        Console.WriteLine("PASS: definition/session template contract checks (5 groups).");
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
