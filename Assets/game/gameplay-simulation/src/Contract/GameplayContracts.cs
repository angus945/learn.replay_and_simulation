using System;
using Invariants;
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
            ulong seed = 814731, string build = "unspecified")
        {
            TickDelta = tickDelta; Speed = speed; Health = health; Damage = damage; AttackRange = attackRange;
            IncludeEnemy = includeEnemy; MaxTicks = maxTicks; MaxActions = maxActions; TraceCapacity = traceCapacity;
            Seed = seed; Build = build;
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
        public void Validate()
        {
            if (!Finite(TickDelta) || TickDelta <= 0 || !Finite(Speed) || Speed < 0 || Health <= 0 || Damage <= 0 ||
                !Finite(AttackRange) || AttackRange <= 0 || MaxTicks < 1 || MaxActions < 1 || TraceCapacity < 1 || string.IsNullOrWhiteSpace(Build))
                throw new ArgumentException("Invalid scenario configuration.");
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
        public GameplayObservation(ulong tick, IEnumerable<ActorObservation> actors)
        { Tick = tick; Actors = new List<ActorObservation>(actors).AsReadOnly(); }
        public ulong Tick { get; }
        public IReadOnlyList<ActorObservation> Actors { get; }
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
            GameplayObservation observation = null, string exceptionType = null, string diagnosticPolicy = null)
        {
            SchemaVersion = 1; SessionId = session; Scenario = scenario; FailureTick = tick; ActionSequence = sequence;
            Code = code; Exception = exception; Runtime = Environment.Version + " / " + Environment.OSVersion;
            this.actions = new List<GameplayRequest>(actions).ToArray(); this.results = new List<ActionResult>(results).ToArray();
            this.hashes = new List<HashCheckpoint>(hashes).ToArray(); this.trace = new List<TraceEntry>(trace).ToArray(); DroppedTraceEntries = dropped;
            actors = observation == null ? Array.Empty<ActorObservation>() : new List<ActorObservation>(observation.Actors).ToArray();
            ExceptionType = exceptionType;
            DiagnosticPolicy = diagnosticPolicy;
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
        public IReadOnlyList<GameplayRequest> Actions => Array.AsReadOnly(actions);
        public IReadOnlyList<ActionResult> Results => Array.AsReadOnly(results);
        public IReadOnlyList<HashCheckpoint> Hashes => Array.AsReadOnly(hashes);
        public IReadOnlyList<TraceEntry> Trace => Array.AsReadOnly(trace);
        public IReadOnlyList<ActorObservation> Actors => Array.AsReadOnly(actors ?? Array.Empty<ActorObservation>());
    }
}
