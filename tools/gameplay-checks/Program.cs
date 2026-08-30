using System;
using System.IO;
using System.Linq;
using GameplaySimulation;
using Testability;
using Testability.Templates;
using DebugOverlay;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0) return RecordingCli.Run(args);
            GameplayProtocol.Game.Tests.GameplayProtocolContractChecks.RunAll();
            Console.WriteLine("PASS: protocol payload v2, modern session ports, actual drive ownership and idempotent operations (10 groups).");
            FrameworkGuideExamples.Run();
            CheckLifecycle();
            CheckRecordingAndDiagnostics();
            CheckFailureRecording();
            Console.WriteLine("PASS: modern headless gameplay, ordered hashes, success/failure recording replay and read-only diagnostic consumer. No Unity assemblies.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckRecordingAndDiagnostics()
    {
        GameplayDefinition definition = new GameplayDefinition();
        GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
        (ulong Sequence, ulong Tick, GameplayInput Input)[] actions = {
            (1, 1, new GameplayInput(GameplayActionKind.Attack, 1, 2)),
            (2, 2, new GameplayInput(GameplayActionKind.Attack, 1, 2)),
            (3, 3, new GameplayInput(GameplayActionKind.Attack, 1, 2)),
            (4, 3, new GameplayInput(GameplayActionKind.Move, 2, x: 1))
        };
        using (ModernSession first = definition.CreateTestSession(scenario))
        using (ModernSession second = definition.CreateTestSession(scenario))
        {
            foreach ((ulong Sequence, ulong Tick, GameplayInput Input) action in actions)
                Require(first.Submit(first.Id, action.Sequence, action.Tick, action.Input).Queued, "forward admission failed");
            foreach ((ulong Sequence, ulong Tick, GameplayInput Input) action in actions.Reverse())
                Require(second.Submit(second.Id, action.Sequence, action.Tick, action.Input).Queued, "reverse admission failed");
            for (int tick = 0; tick < 8; tick++)
                Require(first.Step().Hash == second.Step().Hash, "hash sequence mismatch");
            Require(!first.Observe().FindActor(2).Active, "dead actor remains active");
            Require(first.Results.Find(first.Id, 4).Result.Code == "actor.dead", "dead actor move not rejected");
            ReadOnlyDiagnosticsModel<GameplayObservation> panel = new ReadOnlyDiagnosticsModel<GameplayObservation>(first.Diagnostics);
            panel.Poll();
            int cached = panel.History.Count;
            panel.Poll();
            Require(first.CurrentTick == 8 && panel.History.Count == cached, "diagnostic polling altered state or duplicated entries");
            Require(!(first.Diagnostics is ITemplateGameplay<GameplayInput, GameplayObservation>) &&
                !(first.Diagnostics is ITemplateSimulation) && !(first.Diagnostics is ITemplateAdmin<GameplayScenario>),
                "diagnostics exposed mutation ports");
            using (ModernReplay replay = definition.CreateReplay(RoundTrip(first.CaptureRecording())))
            {
                replay.Play();
                for (int frame = 0; frame < 1000 && replay.State == TemplateReplayState.Playing; frame++) replay.AdvanceTime(1f / 144);
                Require(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null, "normal replay mismatch");
            }
        }
    }

    private static void CheckFailureRecording()
    {
        GameplayDefinition definition = new GameplayDefinition();
        GameplayScenario scenario = new GameplayScenario(tickDelta: 2, speed: float.MaxValue,
            build: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD") ?? "headless-check-source");
        using (ModernSession failed = definition.CreateTestSession(scenario))
        {
            Require(failed.Submit(failed.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, 1, x: 1)).Queued, "failure input rejected");
            failed.Step();
            Require(failed.State == SessionState.Faulted, "overflow was not captured");
            using (ModernReplay replay = definition.CreateReplay(RoundTrip(failed.CaptureRecording())))
            {
                replay.Step();
                Require(replay.State == TemplateReplayState.ReproducedFailure && replay.FirstDifference == null,
                    "failure recording does not reproduce failure/results/hashes");
            }
        }
    }

    private static TemplateRecording RoundTrip(TemplateRecording recording)
    {
        using (MemoryStream bytes = new MemoryStream())
        {
            TemplateRecordingIO.Write(bytes, recording); bytes.Position = 0;
            return TemplateRecordingIO.Read(bytes);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckLifecycle()
    {
        GameplayDefinition definition = new GameplayDefinition();
        using (ModernSession session = definition.CreateTestSession(new GameplayScenario(tickDelta: .125f, damage: 100,
            respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40)))
        {
            for (ulong tick = 1; tick <= 8; tick++)
            {
                Require(session.Submit(session.Id, tick, tick, new GameplayInput(GameplayActionKind.Attack, 1, tick + 1)).Queued, "lifecycle input rejected");
                session.Step();
            }
            LifecycleSnapshot lifecycle = session.Observe().Lifecycle;
            Require(lifecycle.EnemiesSpawned == 9 && lifecycle.Active == 2, "lifecycle mismatch");
            using (ModernReplay replay = definition.CreateReplay(RoundTrip(session.CaptureRecording())))
            {
                replay.Play();
                for (int frame = 0; frame < 1000 && replay.State == TemplateReplayState.Playing; frame++) replay.AdvanceTime(1f / 144);
                Require(replay.State == TemplateReplayState.Completed, "random respawn replay diverged");
            }
            Console.WriteLine("PASS: modern runtime respawn, lifecycle consistency, seeded enemy health and replay.");
        }
    }

}
