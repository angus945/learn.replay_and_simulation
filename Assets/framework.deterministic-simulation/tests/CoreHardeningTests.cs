using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class CoreHardeningTests
    {
        [Test] public void LowLevelClockAndFailure() => CoreHardeningContractChecks.LowLevelClockAndFailure();
        [Test] public void LowLevelReentryAndRenderFailure() => CoreHardeningContractChecks.LowLevelReentryAndRenderFailure();
        [Test] public void SessionOwnerThread() => CoreHardeningContractChecks.SessionOwnerThread();
        [Test] public void ParticipantOrderAndReactionTiming() => CoreHardeningContractChecks.ParticipantOrderAndReactionTiming();
    }
}
