using NUnit.Framework;

namespace GameplaySimulation.Tests
{
    public sealed class ModernGameplayTests
    {
        [Test] public void ScenarioLimits() => ModernGameplayContractChecks.ScenarioLimits();
        [Test] public void CustomInvariantIsolationAndPolicy() => ModernGameplayContractChecks.CustomInvariantIsolationAndPolicy();
        [Test] public void EventCausation() => ModernGameplayContractChecks.EventCausation();
        [Test] public void DiagonalMovementAndReplay() => ModernGameplayContractChecks.DiagonalMovementAndReplay();
    }
}
