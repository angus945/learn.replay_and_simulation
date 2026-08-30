using NUnit.Framework;

namespace GameplaySimulation.Tests
{
    public sealed class DemoTemplateTests
    {
        [Test] public void DemoUsesTemplateWithGameplayParityAndReplay() => DemoTemplateChecks.Verify();
    }
}
