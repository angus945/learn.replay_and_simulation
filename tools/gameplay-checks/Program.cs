using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameplaySimulation;
using Testability;
using DebugOverlay;
using GameplayProtocol;
using GameplayProtocol.Game;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0) return RecordingCli.Run(args);
            CheckProtocol();
            FrameworkGuideExamples.Run();
            CheckLifecycle();
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
            ReplayArtifact recording = first.CaptureReplay();
            using (MemoryStream replayBytes = new MemoryStream())
            {
                ArtifactJson.Write(replayBytes, recording); replayBytes.Position = 0;
                ReplayPlayback playback = new ReplayPlayback(ArtifactJson.Read<ReplayArtifact>(replayBytes));
                playback.Play();
                for (int frame = 0; frame < 1000 && playback.State == ReplayPlaybackState.Playing; frame++) playback.AdvanceTime(1f / 144);
                Require(playback.State == ReplayPlaybackState.Completed && playback.FirstDifference == null, "normal replay mismatch");
            }

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
            }
            const string previousArtifact = "docs/testability/failure-example.json";
            if (File.Exists(previousArtifact))
                using (FileStream input = File.OpenRead(previousArtifact))
                    Require(ScenarioRerun.VerifyFailure(ArtifactJson.Read<FailureArtifact>(input)), "previous artifact compatibility failed");
            Console.WriteLine("PASS: headless gameplay, ordered hashes, artifact rerun/compatibility, normal replay, read-only diagnostic consumer. No Unity assemblies.");
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

    private static void CheckProtocol()
    {
        GameplaySession target = new GameplaySession(); target.Start(new GameplayScenario(tickDelta: .25f));
        GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target);
        ProtocolClient client = new ProtocolClient("headless", ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive);
        Require(SendProtocol(adapter, client, new ProtocolRequest(1, "claim", target.Id, "control.acquire")).Success, "protocol claim failed");
        string action = ProtocolJson.Write(new ActionDto { Sequence = "1", TargetTick = "1", Kind = "Move", Actor = "1", X = 1 });
        Require(SendProtocol(adapter, client, new ProtocolRequest(1, "move", target.Id, "action.submit", action)).Success, "protocol submit failed");
        ProtocolRequest step = new ProtocolRequest(1, "step", target.Id, "simulation.step");
        ProtocolResponse first = SendProtocol(adapter, client, step);
        Require(first.Success && SendProtocol(adapter, client, step).PayloadJson == first.PayloadJson && target.CurrentTick == 1, "protocol retry advanced twice");
        Require(target.Observe().Actors[0].X == 1, "protocol movement mismatch");
        Console.WriteLine("PASS: protocol JSON round trip, control authority, queued dispatch and idempotent Step.");
    }
    private static void CheckLifecycle()
    {
        GameplaySession session = new GameplaySession();
        session.Start(new GameplayScenario(tickDelta: .125f, damage: 100, respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40));
        for (ulong tick = 1; tick <= 8; tick++)
        {
            session.Submit(new GameplayRequest(session.Id, tick, tick, GameplayActionKind.Attack, 1, tick + 1)); session.Step();
        }
        Require(session.ObserveLifecycle().EnemiesSpawned == 9 && session.ObserveLifecycle().Active == 2, "lifecycle mismatch");
        using (MemoryStream bytes = new MemoryStream())
        {
            ArtifactJson.Write(bytes, session.CaptureReplay()); bytes.Position = 0;
            ReplayPlayback playback = new ReplayPlayback(ArtifactJson.Read<ReplayArtifact>(bytes)); playback.Play();
            for (int frame = 0; frame < 1000 && playback.State == ReplayPlaybackState.Playing; frame++) playback.AdvanceTime(1f / 144);
            Require(playback.State == ReplayPlaybackState.Completed, "random respawn replay diverged");
        }
        Console.WriteLine("PASS: runtime respawn, lifecycle consistency, seeded enemy health and replay.");
    }
    private static ProtocolResponse SendProtocol(GameplayProtocolAdapter adapter, ProtocolClient client, ProtocolRequest request)
    {
        ProtocolRequest decoded = ProtocolJson.Read<ProtocolRequest>(ProtocolJson.Write(request));
        System.Threading.Tasks.Task<ProtocolResponse> response = adapter.Endpoint.Enqueue(client, decoded);
        Require(!response.IsCompleted, "protocol executed before owner pump");
        adapter.Endpoint.Drain(1);
        return ProtocolJson.Read<ProtocolResponse>(ProtocolJson.Write(response.Result));
    }
}
