using System;
using System.IO;
using Arena.Composition;
using Arena.Integration;
using Arena.Tests;
using Testability.Templates;

internal static class Program
{
    private static readonly string[] Names = { "domain", "application", "simulation", "input", "lifecycle", "observation", "diagnostics", "replay", "realtime" };
    private static readonly Action[] Checks = { ArenaContractChecks.Domain, ArenaContractChecks.Application, ArenaContractChecks.Simulation,
        ArenaContractChecks.Input, ArenaContractChecks.Lifecycle, ArenaContractChecks.Observation, ArenaContractChecks.Diagnostics,
        ArenaContractChecks.Replay, ArenaContractChecks.Realtime };
    private static int Main(string[] args)
    {
        try
        {
            string command = args.Length == 0 ? "all" : args[0];
            if (command == "capture" || command == "capture-failure")
            {
                if (args.Length != 2) return Usage();
                TemplateRecording recording = ArenaContractChecks.CreateRecording(command == "capture-failure");
                using (FileStream stream = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write)) TemplateRecordingIO.Write(stream, recording);
                Console.WriteLine("Saved " + recording.Ticks.Count + " ticks to " + Path.GetFullPath(args[1])); return 0;
            }
            if (command == "rerun")
            {
                if (args.Length != 2) return Usage();
                TemplateRecording recording;
                using (FileStream stream = File.OpenRead(args[1])) recording = TemplateRecordingIO.Read(stream);
                if (recording.Policy != ArenaDefinition.DefaultPolicy && recording.Policy != new ArenaDefinition(true).PolicyId)
                    throw new ArgumentException("Unknown Arena policy. Select an explicit supported composition.");
                using (TemplateReplay<ArenaRuntime, ArenaScenario, ArenaInput, ArenaObservation> replay =
                    new ArenaDefinition(recording.Policy != ArenaDefinition.DefaultPolicy).CreateReplay(recording))
                {
                    while (replay.State == TemplateReplayState.Paused) replay.Step();
                    Console.WriteLine(replay.State + " tick=" + replay.CurrentTick + (replay.FirstDifference == null ? "" : " difference=" + replay.FirstDifference.Category));
                    return replay.State == TemplateReplayState.Completed || replay.State == TemplateReplayState.ReproducedFailure ? 0 : 1;
                }
            }
            if (args.Length > 1) return Usage();
            int selected = Array.IndexOf(Names, command);
            if (command != "all" && selected < 0) return Usage();
            for (int index = 0; index < Checks.Length; index++)
            {
                if (command != "all" && index != selected) continue;
                Checks[index](); Console.WriteLine("PASS " + Names[index]);
            }
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static int Usage()
    {
        Console.Error.WriteLine("arena-checks [all|" + string.Join("|", Names) + "]\narena-checks capture|capture-failure|rerun <file.json>");
        return 2;
    }
}
