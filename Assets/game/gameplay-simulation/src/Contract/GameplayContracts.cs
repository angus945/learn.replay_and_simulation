using System;
using InvariantChecks;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Testability;

namespace GameplaySimulation
{
    public enum GameplayActionKind { Move, Attack }

    /// <summary>Immutable external intent record. Raw values allow invalid requests to receive structured results.</summary>
    [DataContract]
    public sealed class GameplayRequest
    {
        public GameplayRequest(string sessionId, ulong sequence, ulong targetTick, GameplayActionKind kind, ulong actor, ulong target = 0, float x = 0, float y = 0)
        { SessionId = sessionId; Sequence = sequence; TargetTick = targetTick; Kind = kind; Actor = actor; Target = target; X = x; Y = y; }
        [DataMember(Order = 1)] public string SessionId { get; private set; }
        [DataMember(Order = 2)] public ulong Sequence { get; private set; }
        [DataMember(Order = 3)] public ulong TargetTick { get; private set; }
        [DataMember(Order = 4)] public GameplayActionKind Kind { get; private set; }
        [DataMember(Order = 5)] public ulong Actor { get; private set; }
        [DataMember(Order = 6)] public ulong Target { get; private set; }
        [DataMember(Order = 7)] public float X { get; private set; }
        [DataMember(Order = 8)] public float Y { get; private set; }
        public GameplayRequest InSession(string id) => new GameplayRequest(id, Sequence, TargetTick, Kind, Actor, Target, X, Y);
    }

    [DataContract]
    public sealed class GameplayScenario
    {
        public GameplayScenario(float tickDelta = 1f / 60f, float speed = 4, int health = 30,
            int damage = 10, float attackRange = 2, bool includeEnemy = true,
            int maxTicks = 36000, int maxActions = 40000, int traceCapacity = 512,
            ulong seed = 814731, string build = "unspecified", bool respawnEnemies = false,
            int enemyHealthMin = 0, int enemyHealthMax = 0, int maxEnemySpawns = 128, bool randomRespawnDelay = false)
        {
            TickDelta = tickDelta; Speed = speed; Health = health; Damage = damage; AttackRange = attackRange;
            IncludeEnemy = includeEnemy; MaxTicks = maxTicks; MaxActions = maxActions; TraceCapacity = traceCapacity;
            Seed = seed; Build = build;
            RandomRespawnDelay = randomRespawnDelay;
            RespawnEnemies = respawnEnemies; EnemyHealthMin = enemyHealthMin; EnemyHealthMax = enemyHealthMax; MaxEnemySpawns = maxEnemySpawns;
            Validate();
        }
        [DataMember(Order = 1)] public float TickDelta { get; private set; }
        [DataMember(Order = 2)] public float Speed { get; private set; }
        [DataMember(Order = 3)] public int Health { get; private set; }
        [DataMember(Order = 4)] public int Damage { get; private set; }
        [DataMember(Order = 5)] public float AttackRange { get; private set; }
        [DataMember(Order = 6)] public bool IncludeEnemy { get; private set; }
        [DataMember(Order = 7)] public int MaxTicks { get; private set; }
        [DataMember(Order = 8)] public int MaxActions { get; private set; }
        [DataMember(Order = 9)] public int TraceCapacity { get; private set; }
        [DataMember(Order = 10)] public ulong Seed { get; private set; }
        [DataMember(Order = 11)] public string Build { get; private set; }
        [DataMember(Order = 12)] public bool RespawnEnemies { get; private set; }
        [DataMember(Order = 13)] public int EnemyHealthMin { get; private set; }
        [DataMember(Order = 14)] public int EnemyHealthMax { get; private set; }
        [DataMember(Order = 15)] public int MaxEnemySpawns { get; private set; }
        [DataMember(Order = 16)] public bool RandomRespawnDelay { get; private set; }
        public bool RandomEnemyHealth => EnemyHealthMin != 0 || EnemyHealthMax != 0;
        public bool ExtendedLifecycle => RespawnEnemies || RandomEnemyHealth;
        public void Validate()
        {
            if (!Finite(TickDelta) || TickDelta <= 0 || !Finite(Speed) || Speed < 0 || Health <= 0 || Damage <= 0 ||
                !Finite(AttackRange) || AttackRange <= 0 || MaxTicks < 1 || MaxTicks > 100000 || MaxActions < 1 || MaxActions > 100000 ||
                TraceCapacity < 1 || TraceCapacity > 65536 || string.IsNullOrWhiteSpace(Build))
                throw new ArgumentException("Invalid scenario configuration.");
            if (RandomEnemyHealth && (EnemyHealthMin < 1 || EnemyHealthMax < EnemyHealthMin || EnemyHealthMax == int.MaxValue))
                throw new ArgumentException("Invalid inclusive enemy health range.");
            if (ExtendedLifecycle && (MaxEnemySpawns < 1 || MaxEnemySpawns > 4096)) throw new ArgumentException("Enemy spawn budget must be 1..4096.");
            if (RandomRespawnDelay && (!RespawnEnemies || TickDelta > 3 || 3d / TickDelta >= int.MaxValue))
                throw new ArgumentException("Random respawn requires respawning and a representable 1..3 second tick range.");
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DataContract]
    public sealed class ActorObservation
    {
        public ActorObservation(ulong id, float x, float y, float dx, float dy, float speed, int health, int maxHealth, bool active)
        { Id = id; X = x; Y = y; DirectionX = dx; DirectionY = dy; Speed = speed; Health = health; MaxHealth = maxHealth; Active = active; }
        [DataMember(Order = 1)] public ulong Id { get; private set; }
        [DataMember(Order = 2)] public float X { get; private set; }
        [DataMember(Order = 3)] public float Y { get; private set; }
        [DataMember(Order = 4)] public float DirectionX { get; private set; }
        [DataMember(Order = 5)] public float DirectionY { get; private set; }
        [DataMember(Order = 6)] public float Speed { get; private set; }
        [DataMember(Order = 7)] public int Health { get; private set; }
        [DataMember(Order = 8)] public int MaxHealth { get; private set; }
        [DataMember(Order = 9)] public bool Active { get; private set; }
    }

    public sealed class GameplayObservation
    {
        public GameplayObservation(ulong tick, IEnumerable<ActorObservation> actors, ulong enemyRandomState = 0, int enemiesSpawned = 0,
            ulong respawnRandomState = 0, IEnumerable<ulong> pendingRespawnTicks = null, ulong playerId = 1, LifecycleSnapshot lifecycle = null)
        { Tick = tick; Actors = new List<ActorObservation>(actors).AsReadOnly(); EnemyRandomState = enemyRandomState; EnemiesSpawned = enemiesSpawned;
            RespawnRandomState = respawnRandomState; PendingRespawnTicks = new List<ulong>(pendingRespawnTicks ?? Array.Empty<ulong>()).AsReadOnly();
            PlayerId = playerId; Lifecycle = lifecycle; }
        public ulong Tick { get; }
        public IReadOnlyList<ActorObservation> Actors { get; }
        public ulong EnemyRandomState { get; }
        public int EnemiesSpawned { get; }
        public ulong RespawnRandomState { get; }
        public IReadOnlyList<ulong> PendingRespawnTicks { get; }
        public ulong PlayerId { get; }
        public LifecycleSnapshot Lifecycle { get; }
        public ActorObservation FindActor(ulong id)
        {
            foreach (ActorObservation actor in Actors) if (actor.Id == id) return actor;
            return null;
        }
    }

    public sealed class TickReport
    {
        public TickReport(ulong tick, IEnumerable<ActionResult> results, string hash, IEnumerable<InvariantViolation> violations)
        { Tick = tick; Results = new List<ActionResult>(results).AsReadOnly(); StateHash = hash; Violations = new List<InvariantViolation>(violations).AsReadOnly(); }
        public ulong Tick { get; }
        public IReadOnlyList<ActionResult> Results { get; }
        public string StateHash { get; }
        public IReadOnlyList<InvariantViolation> Violations { get; }
    }

    [DataContract]
    public sealed class HashCheckpoint
    {
        public HashCheckpoint(ulong tick, string hash) { Tick = tick; Hash = hash; }
        [DataMember(Order = 1)] public ulong Tick { get; private set; }
        [DataMember(Order = 2)] public string Hash { get; private set; }
    }

    [DataContract]
    public sealed class FailureArtifact
    {
        public FailureArtifact(string session, GameplayScenario scenario, ulong tick, ulong sequence, string code,
            string exception, IEnumerable<GameplayRequest> actions, IEnumerable<ActionResult> results,
            IEnumerable<HashCheckpoint> hashes, IEnumerable<TraceEntry> trace, long dropped,
            GameplayObservation observation = null, string exceptionType = null, string diagnosticPolicy = null,
            string failureStage = null, ulong lastCompletedTick = 0)
        {
            SchemaVersion = 1; SessionId = session; Scenario = scenario; FailureTick = tick; ActionSequence = sequence;
            Code = code; Exception = exception; Runtime = Environment.Version + " / " + Environment.OSVersion;
            this.actions = new List<GameplayRequest>(actions).ToArray(); this.results = new List<ActionResult>(results).ToArray();
            this.hashes = new List<HashCheckpoint>(hashes).ToArray(); this.trace = new List<TraceEntry>(trace).ToArray(); DroppedTraceEntries = dropped;
            actors = observation == null ? Array.Empty<ActorObservation>() : new List<ActorObservation>(observation.Actors).ToArray();
            ExceptionType = exceptionType;
            DiagnosticPolicy = diagnosticPolicy;
            FailureStage = failureStage; LastCompletedTick = lastCompletedTick;
        }
        [DataMember(Order = 1)] public int SchemaVersion { get; private set; }
        [DataMember(Order = 2)] public string SessionId { get; private set; }
        [DataMember(Order = 3)] public GameplayScenario Scenario { get; private set; }
        [DataMember(Order = 4)] public ulong FailureTick { get; private set; }
        [DataMember(Order = 5)] public ulong ActionSequence { get; private set; }
        [DataMember(Order = 6)] public string Code { get; private set; }
        [DataMember(Order = 7)] public string Exception { get; private set; }
        [DataMember(Order = 8)] public string Runtime { get; private set; }
        [DataMember(Order = 9)] private GameplayRequest[] actions;
        [DataMember(Order = 10)] private ActionResult[] results;
        [DataMember(Order = 11)] private HashCheckpoint[] hashes;
        [DataMember(Order = 12)] private TraceEntry[] trace;
        [DataMember(Order = 13)] public long DroppedTraceEntries { get; private set; }
        [DataMember(Order = 14)] private ActorObservation[] actors;
        [DataMember(Order = 15)] public string ExceptionType { get; private set; }
        [DataMember(Order = 16, EmitDefaultValue = false)] public string DiagnosticPolicy { get; private set; }
        [DataMember(Order = 17, EmitDefaultValue = false)] public string FailureStage { get; private set; }
        [DataMember(Order = 18)] public ulong LastCompletedTick { get; private set; }
        public IReadOnlyList<GameplayRequest> Actions => Array.AsReadOnly(actions);
        public IReadOnlyList<ActionResult> Results => Array.AsReadOnly(results);
        public IReadOnlyList<HashCheckpoint> Hashes => Array.AsReadOnly(hashes);
        public IReadOnlyList<TraceEntry> Trace => Array.AsReadOnly(trace);
        public IReadOnlyList<ActorObservation> Actors => Array.AsReadOnly(actors ?? Array.Empty<ActorObservation>());
    }
}
