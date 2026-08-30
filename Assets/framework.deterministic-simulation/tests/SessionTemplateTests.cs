using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class SessionTemplateTests
    {
        [Test] public void Lifecycle() => SessionTemplateContractChecks.Lifecycle();
        [Test] public void MissingConfiguration() => SessionTemplateContractChecks.MissingConfiguration();
        [Test] public void FaultAndReentry() => SessionTemplateContractChecks.FaultAndReentry();
        [Test] public void ResetFailures() => SessionTemplateContractChecks.ResetFailures();
        [Test] public void IndependentSessions() => SessionTemplateContractChecks.IndependentSessions();
    }
}
