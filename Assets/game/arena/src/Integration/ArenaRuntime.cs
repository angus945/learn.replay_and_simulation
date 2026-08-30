using Arena.Application;
using Arena.Infrastructure;

namespace Arena.Integration
{
    /// <summary>Session-owned resources, not an aggregate and not a service locator for hosts.</summary>
    public sealed class ArenaRuntime
    {
        public ArenaRuntime(ArenaScenario scenario)
        {
            TickDelta = scenario.TickDelta;
            ActorRepository repository = new ActorRepository();
            Lifecycle = new RegistryLifecycle(repository);
            Random = new SpawnRandom(scenario.Seed);
            Application = new ArenaApplication(repository, Lifecycle, Random, scenario.CreateRules());
        }
        public ArenaApplication Application { get; }
        public RegistryLifecycle Lifecycle { get; }
        public SpawnRandom Random { get; }
        public float TickDelta { get; }
    }
}
