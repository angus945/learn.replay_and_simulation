using NUnit.Framework;

namespace Arena.Tests
{
    public sealed class ArenaIntegrationTests
    {
        [Test] public void Domain() => ArenaContractChecks.Domain();
        [Test] public void Application() => ArenaContractChecks.Application();
        [Test] public void Simulation() => ArenaContractChecks.Simulation();
        [Test] public void Input() => ArenaContractChecks.Input();
        [Test] public void Lifecycle() => ArenaContractChecks.Lifecycle();
        [Test] public void Observation() => ArenaContractChecks.Observation();
        [Test] public void Diagnostics() => ArenaContractChecks.Diagnostics();
        [Test] public void Replay() => ArenaContractChecks.Replay();
        [Test] public void Realtime() => ArenaContractChecks.Realtime();
    }
}
