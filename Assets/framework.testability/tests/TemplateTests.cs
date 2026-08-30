using NUnit.Framework;

namespace Testability.Tests
{
    public sealed class TemplateTests
    {
        [Test] public void AdmissionAndDiagnostics() => TemplateContractChecks.AdmissionAndDiagnostics();
        [Test] public void OrderingResetAndLimits() => TemplateContractChecks.OrderingResetAndLimits();
        [Test] public void ReplayFrameMatrix() => TemplateContractChecks.ReplayFrameMatrix();
        [Test] public void FailureReplay() => TemplateContractChecks.FailureReplay();
        [Test] public void InvariantAndCaptureFailures() => TemplateContractChecks.InvariantAndCaptureFailures();
        [Test] public void DivergenceAndMalformedRecording() => TemplateContractChecks.DivergenceAndMalformedRecording();
        [Test] public void ThreadAndReentry() => TemplateContractChecks.ThreadAndReentry();
        [Test] public void PhaseAndFileBounds() => TemplateContractChecks.PhaseAndFileBounds();
    }
}
