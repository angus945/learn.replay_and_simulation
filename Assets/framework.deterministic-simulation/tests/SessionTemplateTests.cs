using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class SessionTemplateTests
    {
        [Test] public void RealtimeTimingAndOwnership() => SessionTemplateContractChecks.RealtimeTimingAndOwnership();
        [Test] public void RealtimeFailuresAndReentry() => SessionTemplateContractChecks.RealtimeFailuresAndReentry();
        [Test] public void Lifecycle() => SessionTemplateContractChecks.Lifecycle();
        [Test] public void MissingConfiguration() => SessionTemplateContractChecks.MissingConfiguration();
        [Test] public void FaultAndReentry() => SessionTemplateContractChecks.FaultAndReentry();
        [Test] public void ResetFailures() => SessionTemplateContractChecks.ResetFailures();
        [Test] public void IndependentSessions() => SessionTemplateContractChecks.IndependentSessions();
    }
}
