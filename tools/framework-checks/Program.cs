using System;
using DeterministicSimulation.Framework.Tests;
using Testability.Tests;
using WaveDispatching.Tests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            WaveDispatcherContractChecks.CallbackGuardsPreserveQueuedItems();
            WaveDispatcherContractChecks.CallbackFailureClearsWorkAndReleasesGuard();
            CoreHardeningContractChecks.LowLevelClockAndFailure();
            CoreHardeningContractChecks.LowLevelReentryAndRenderFailure();
            CoreHardeningContractChecks.SessionOwnerThread();
            CoreHardeningContractChecks.ParticipantOrderAndReactionTiming();
            Console.WriteLine("PASS framework core / message reactions / failure boundaries");
            SessionTemplateContractChecks.Lifecycle();
            SessionTemplateContractChecks.MissingConfiguration();
            SessionTemplateContractChecks.FaultAndReentry();
            SessionTemplateContractChecks.ResetFailures();
            SessionTemplateContractChecks.IndependentSessions();
            SessionTemplateContractChecks.RealtimeTimingAndOwnership();
            SessionTemplateContractChecks.RealtimeFailuresAndReentry();
            Console.WriteLine("PASS framework definition / session / realtime ownership");
            TemplateContractChecks.AdmissionAndDiagnostics();
            TemplateContractChecks.OrderingResetAndLimits();
            TemplateContractChecks.ReplayFrameMatrix();
            TemplateContractChecks.FailureReplay();
            TemplateContractChecks.InvariantAndCaptureFailures();
            TemplateContractChecks.DivergenceAndMalformedRecording();
            TemplateContractChecks.ThreadAndReentry();
            TemplateContractChecks.PhaseAndFileBounds();
            TemplateContractChecks.MetadataCausationAndResultPages();
            TemplateContractChecks.PolicyAndReplaySetupFailures();
            TemplateContractChecks.RealtimeRecordingAndOwnership();
            Console.WriteLine("PASS framework testability / diagnostics / recording / replay (no Game assembly)");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
