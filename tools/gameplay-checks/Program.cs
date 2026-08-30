using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameplaySimulation;
using Testability;
using DebugOverlay;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            GameplayRequest[] actions = {
                new GameplayRequest("source", 1, 1, GameplayActionKind.Attack, 1, 2),
                new GameplayRequest("source", 2, 2, GameplayActionKind.Attack, 1, 2),
                new GameplayRequest("source", 3, 3, GameplayActionKind.Attack, 1, 2),
                new GameplayRequest("source", 4, 3, GameplayActionKind.Move, 2, x: 1)
            };
            GameplaySession first = new GameplaySession();
            IReadOnlyList<TickReport> a = ScenarioRerun.Run(scenario, actions, 8, first);
            IReadOnlyList<TickReport> b = ScenarioRerun.Run(scenario, actions.Reverse(), 8);
            Require(a.Select(tick => tick.StateHash).SequenceEqual(b.Select(tick => tick.StateHash)), "hash sequence mismatch");
            Require(!first.Observe().Actors[1].Active, "dead actor remains active");
            Require(a[2].Results[1].Code == "actor.dead", "dead actor move not rejected");
            ReadOnlyDiagnosticsModel<GameplayObservation> panel = new ReadOnlyDiagnosticsModel<GameplayObservation>(first.Diagnostics);
            panel.Poll();
            int cached = panel.History.Count;
            panel.Poll();
            Require(first.CurrentTick == 8 && panel.History.Count == cached, "diagnostic polling altered state or duplicated entries");
            Require(!(first.Diagnostics is IGameplayControl), "diagnostics exposed gameplay control");

            GameplaySession failed = new GameplaySession();
            failed.Start(new GameplayScenario(tickDelta: 2, speed: float.MaxValue,
                build: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD") ?? "headless-check-source-v1"));
            failed.Submit(new GameplayRequest(failed.Id, 1, 1, GameplayActionKind.Move, 1, x: 1));
            failed.Step();
            Require(failed.State == SessionState.Faulted, "overflow was not captured");
            using (MemoryStream buffer = new MemoryStream())
            {
                ArtifactJson.Write(buffer, failed.Failure);
                buffer.Position = 0;
                FailureArtifact artifact = ArtifactJson.Read<FailureArtifact>(buffer);
                Require(ScenarioRerun.VerifyFailure(artifact), "artifact does not reproduce failure/results/hashes");
                if (args.Length > 0)
                {
                    string path = Path.GetFullPath(args[0]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                        ArtifactJson.Write(output, artifact);
                    Console.WriteLine("Failure example: " + path);
                }
            }
            const string previousArtifact = "docs/testability/failure-example.json";
            if (File.Exists(previousArtifact))
                using (FileStream input = File.OpenRead(previousArtifact))
                    Require(ScenarioRerun.VerifyFailure(ArtifactJson.Read<FailureArtifact>(input)), "previous artifact compatibility failed");
            Console.WriteLine("PASS: headless gameplay, ordered hashes, artifact rerun/compatibility, read-only diagnostic consumer. No Unity assemblies.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
