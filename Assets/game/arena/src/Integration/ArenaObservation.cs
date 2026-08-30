using System.Collections.Generic;
using Arena.Domain;

namespace Arena.Integration
{
    public sealed class ActorSnapshot
    {
        public ActorSnapshot(Actor actor)
        {
            Id = actor.Id.Value; Enemy = actor.Kind == ActorKind.Enemy;
            X = actor.Position.X; Y = actor.Position.Y; DirectionX = actor.Direction.X; DirectionY = actor.Direction.Y;
            Speed = actor.Speed; Health = actor.Health; MaxHealth = actor.MaxHealth;
        }
        public ulong Id { get; }
        public bool Enemy { get; }
        public float X { get; }
        public float Y { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float Speed { get; }
        public int Health { get; }
        public int MaxHealth { get; }
    }
    /// <summary>Detached read model plus explicit future-affecting state. Never exposes an aggregate.</summary>
    public sealed class ArenaObservation
    {
        public ArenaObservation(ArenaRuntime runtime)
        {
            Tick = runtime.Application.Tick; PlayerId = runtime.Application.PlayerId.Value;
            Rules = runtime.Application.Rules; TickDelta = runtime.TickDelta;
            LastActorId = runtime.Application.LastActorId; EnemiesSpawned = runtime.Application.EnemiesSpawned;
            HealthRandomState = runtime.Random.HealthState; DelayRandomState = runtime.Random.DelayState;
            PendingRespawnTicks = new List<ulong>(runtime.Application.PendingRespawnTicks).AsReadOnly();
            RegistryEvidence = new List<ulong>(runtime.Lifecycle.CaptureEvidence()).AsReadOnly();
            RegistryActiveCount = runtime.Lifecycle.ActiveCount;
            List<ActorSnapshot> actors = new List<ActorSnapshot>();
            foreach (Actor actor in runtime.Application.Actors) actors.Add(new ActorSnapshot(actor));
            Actors = actors.AsReadOnly();
        }
        public ulong Tick { get; }
        public ArenaRules Rules { get; }
        public float TickDelta { get; }
        public ulong PlayerId { get; }
        public ulong LastActorId { get; }
        public int EnemiesSpawned { get; }
        public ulong HealthRandomState { get; }
        public ulong DelayRandomState { get; }
        public int RegistryActiveCount { get; }
        public IReadOnlyList<ulong> PendingRespawnTicks { get; }
        public IReadOnlyList<ulong> RegistryEvidence { get; }
        public IReadOnlyList<ActorSnapshot> Actors { get; }
        public ActorSnapshot FindActor(ulong id)
        {
            foreach (ActorSnapshot actor in Actors) if (actor.Id == id) return actor;
            return null;
        }
    }
}
