using NUnit.Framework;

namespace GameplayProtocol.Game.Tests
{
    public sealed class GameplayProtocolTests
    {
        [Test] public void JsonProtocolMatchesDirectGameplayAndStepRetryDoesNotAdvance()
            => GameplayProtocolContractChecks.JsonProtocolMatchesDirectGameplayAndStepRetryDoesNotAdvance();

        [Test] public void ReaderCannotClaimOrMutateButCanDiscoverAndObserve()
            => GameplayProtocolContractChecks.ReaderCannotClaimOrMutateButCanDiscoverAndObserve();

        [Test] public void ResetRetryReturnsOriginalNewIdentityAndRequiresNewLease()
            => GameplayProtocolContractChecks.ResetRetryReturnsOriginalNewIdentityAndRequiresNewLease();

        [Test] public void RealtimeSessionIsReadOnlyThroughAdapter()
            => GameplayProtocolContractChecks.RealtimeSessionIsReadOnlyThroughAdapter();

        [Test] public void ResultsDiagnosticsAndTraceAreMappedToIndependentDtos()
            => GameplayProtocolContractChecks.ResultsDiagnosticsAndTraceAreMappedToIndependentDtos();

        [Test] public void BadPayloadAndCursorAreStructuredErrors()
            => GameplayProtocolContractChecks.BadPayloadAndCursorAreStructuredErrors();

        [Test] public void PayloadVersionIsExplicitAndOldClientsCannotMutate()
            => GameplayProtocolContractChecks.PayloadVersionIsExplicitAndOldClientsCannotMutate();

        [Test] public void CapabilitiesReportActualLimitsAndModernPolicy()
            => GameplayProtocolContractChecks.CapabilitiesReportActualLimitsAndModernPolicy();

        [Test] public void ModernAdmissionCodesAndExecutionResultsRemainDistinct()
            => GameplayProtocolContractChecks.ModernAdmissionCodesAndExecutionResultsRemainDistinct();

        [Test] public void RuntimeDriveOwnershipChangesAreCheckedAtExecution()
            => GameplayProtocolContractChecks.RuntimeDriveOwnershipChangesAreCheckedAtExecution();
    }
}
