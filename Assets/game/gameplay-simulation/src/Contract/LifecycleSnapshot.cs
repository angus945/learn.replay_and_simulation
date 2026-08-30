namespace GameplaySimulation
{
    public sealed class LifecycleSnapshot
    {
        public LifecycleSnapshot(int active, int repositoryCount, int retainedActors, int enemiesSpawned, int pendingSpawns)
        { Active = active; RepositoryCount = repositoryCount; RetainedActors = retainedActors; EnemiesSpawned = enemiesSpawned; PendingSpawns = pendingSpawns; }
        public int Active { get; }
        public int RepositoryCount { get; }
        public int RetainedActors { get; }
        public int EnemiesSpawned { get; }
        public int PendingSpawns { get; }
    }
}
