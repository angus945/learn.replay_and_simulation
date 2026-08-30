using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using GameplaySimulation;
using InvariantChecks;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

/// <summary>Small local recording client. It uses the same definition and session as the demo.</summary>
internal static class RecordingCli
{
    private const int MaxFileBytes = 32 * 1024 * 1024;
    private const string FailurePolicy = GameplayDefinition.DefaultPolicy + "/cli-position-limit-v1";

    internal static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: capture|capture-success|rerun|legacy-rerun <path.json>");
        string path = Path.GetFullPath(args[1]);
        switch (args[0])
        {
            case "capture": return Capture(path, expectFailure: true);
            case "capture-success": return Capture(path, expectFailure: false);
            case "rerun": return Rerun(path);
            case "legacy-rerun": return LegacyRerun(path);
            default: throw new ArgumentException("Unknown command. Use capture, capture-success, rerun or legacy-rerun.");
        }
    }

    private static int Capture(string path, bool expectFailure)
    {
        GameplayDefinition definition = DefinitionFor(expectFailure ? FailurePolicy : GameplayDefinition.DefaultPolicy);
        GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, includeEnemy: false,
            maxTicks: 16, maxActions: 16, traceCapacity: 128,
            build: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD") ?? "headless-cli-source");
        using (ModernSession session = definition.CreateTestSession(scenario))
        {
            ulong player = session.Observe().PlayerId;
            Submit(session, 1, 1, new GameplayInput(GameplayActionKind.Move, player, x: 1, y: expectFailure ? 0 : 1));
            if (!expectFailure) Submit(session, 2, 4, new GameplayInput(GameplayActionKind.Move, player));
            for (int tick = 0; tick < 8 && session.State == SessionState.Running; tick++) session.Simulation.Step();
            if (expectFailure && (session.Failure == null || session.Failure.Tick != 2 || session.Failure.Code != "cli.position_limit" || session.Failure.ExceptionType != null))
                throw new InvalidOperationException("The non-crash invariant example did not produce its expected failure.");
            if (!expectFailure && session.State != SessionState.Running)
                throw new InvalidOperationException("The successful recording example unexpectedly failed.");
            TemplateRecording recording = session.CaptureRecording();
            recording.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) TemplateRecordingIO.Write(output, recording);
            WriteJson(new
            {
                Format = "TemplateRecording",
                Operation = expectFailure ? "capture" : "capture-success",
                Path = path,
                Policy = recording.Policy,
                EndTick = session.CurrentTick,
                Failure = recording.Failure
            });
            return 0;
        }
    }

    private static int Rerun(string path)
    {
        TemplateRecording recording;
        using (FileStream input = OpenBounded(path)) recording = TemplateRecordingIO.Read(input, MaxFileBytes);
        GameplayDefinition definition = DefinitionFor(recording.Policy);
        using (ModernReplay replay = definition.CreateReplay(recording))
        {
            for (int step = 0; step < recording.Limits.MaxTicks && replay.State == TemplateReplayState.Paused; step++) replay.Step();
            bool matches = replay.State == TemplateReplayState.Completed || replay.State == TemplateReplayState.ReproducedFailure;
            List<string> warnings = new List<string>(replay.Warnings);
            AddBuildWarning(recording, warnings);
            WriteJson(new
            {
                Format = "TemplateRecording",
                Matches = matches,
                State = replay.State.ToString(),
                CurrentTick = replay.CurrentTick,
                EndTick = replay.EndTick,
                Policy = recording.Policy,
                FirstDifference = replay.FirstDifference,
                ExpectedFailureCode = recording.Failure?.Code,
                ObservedFailureCode = replay.Diagnostics.ObserveDiagnostics().FaultCode,
                Warnings = warnings
            });
            return matches ? 0 : 2;
        }
    }

    private static int LegacyRerun(string path)
    {
        using (FileStream input = OpenBounded(path))
        {
            FailureArtifact artifact = ArtifactJson.Read<FailureArtifact>(input);
            if (artifact == null) throw new ArgumentException("Legacy artifact cannot be null.");
            if (artifact.FailureTick > 100000 || artifact.Actions.Count > 100000)
                throw new ArgumentException("Legacy CLI rerun budget is 100,000 ticks/actions, matching the shared runtime limits.");
            RerunReport report = FailureRerun.Compare(artifact, currentBuild: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD"));
            WriteJson(report);
            return report.Matches ? 0 : 2;
        }
    }

    private static GameplayDefinition DefinitionFor(string policy)
    {
        if (policy == FailurePolicy)
            return new GameplayDefinition(new Func<IInvariant<GameplayObservation>>[] { () => new PositionLimit() }, FailurePolicy);
        // Unknown policies compare against the current definition and report Diverged. Never load code from an artifact.
        return new GameplayDefinition();
    }

    private static void AddBuildWarning(TemplateRecording recording, ICollection<string> warnings)
    {
        string currentBuild = Environment.GetEnvironmentVariable("GAMEPLAY_BUILD");
        if (string.IsNullOrWhiteSpace(currentBuild)) { warnings.Add("build.unverified"); return; }
        using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(recording.Scenario)))
        {
            GameplayScenario scenario = ArtifactJson.Read<GameplayScenario>(input);
            if (scenario.Build != currentBuild) warnings.Add("build.mismatch: recorded=" + scenario.Build + "; current=" + currentBuild);
        }
    }

    private static FileStream OpenBounded(string path)
    {
        FileStream input = File.OpenRead(path);
        if (input.Length <= MaxFileBytes) return input;
        input.Dispose();
        throw new ArgumentException("Recording exceeds the 32 MiB CLI limit.");
    }

    private static void Submit(ModernSession session, ulong sequence, ulong tick, GameplayInput input)
    {
        SubmissionResult result = session.Gameplay.Submit(session.Id, sequence, tick, input);
        if (!result.Queued) throw new InvalidOperationException("Example input admission failed: " + result.Code);
    }

    private static void WriteJson<T>(T report)
        => Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

    private sealed class PositionLimit : IInvariant<GameplayObservation>
    {
        public string Code => "cli.position_limit";
        public InvariantViolation Evaluate(GameplayObservation observation)
        {
            ActorObservation player = observation.FindActor(observation.PlayerId);
            return player.X > .5f ? new InvariantViolation(Code, "Demonstration oracle: player crossed x = 0.5.") : null;
        }
    }
}
