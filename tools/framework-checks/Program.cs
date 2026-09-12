using System;
using DeterministicSimulation.Framework.Tests;
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
            ModuleContractChecks.StateSnapshot();
            ModuleContractChecks.RuntimeControl();
            ModuleContractChecks.Oracle();
            ModuleContractChecks.Evidence();
            Console.WriteLine("PASS passive observation / control / oracle / evidence modules");
            HostWorkflowContractChecks.DocumentWorkspace();
            HostWorkflowContractChecks.ImportQueue();
            Console.WriteLine("PASS non-simulation workspace / explicit asynchronous host progress");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
