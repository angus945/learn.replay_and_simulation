using System;
using System.Runtime.Serialization;
using Arena.Application;
using Arena.Domain;

namespace Arena.Integration
{
    /// <summary>Serializable run recipe. Only CreateRules crosses into the application layer.</summary>
    [DataContract]
    public sealed class ArenaScenario
    {
        public ArenaScenario(float tickDelta = 1f / 60f, ulong seed = 814731, int maxTicks = 36000,
            int maxInputs = 80000, int traceCapacity = 512, int respawnMinTicks = 30,
            int respawnMaxTicks = 90, int maxEnemySpawns = 12, int damage = 10,
            float speed = 4f, int enemyHealthMin = 20, int enemyHealthMax = 40)
        {
            TickDelta = tickDelta; Seed = seed; MaxTicks = maxTicks; MaxInputs = maxInputs;
            TraceCapacity = traceCapacity; RespawnMinTicks = respawnMinTicks; RespawnMaxTicks = respawnMaxTicks;
            MaxEnemySpawns = maxEnemySpawns; Damage = damage; Speed = speed;
            EnemyHealthMin = enemyHealthMin; EnemyHealthMax = enemyHealthMax;
            Validate();
        }
        [DataMember(Order = 1)] public float TickDelta { get; private set; }
        [DataMember(Order = 2)] public ulong Seed { get; private set; }
        [DataMember(Order = 3)] public int MaxTicks { get; private set; }
        [DataMember(Order = 4)] public int MaxInputs { get; private set; }
        [DataMember(Order = 5)] public int TraceCapacity { get; private set; }
        [DataMember(Order = 6)] public int RespawnMinTicks { get; private set; }
        [DataMember(Order = 7)] public int RespawnMaxTicks { get; private set; }
        [DataMember(Order = 8)] public int MaxEnemySpawns { get; private set; }
        [DataMember(Order = 9)] public int Damage { get; private set; }
        [DataMember(Order = 10)] public float Speed { get; private set; }
        [DataMember(Order = 11)] public int EnemyHealthMin { get; private set; }
        [DataMember(Order = 12)] public int EnemyHealthMax { get; private set; }
        public ArenaRules CreateRules() => new ArenaRules(speed: Speed, damage: Damage, enemyHealthMin: EnemyHealthMin,
            enemyHealthMax: EnemyHealthMax, maxEnemySpawns: MaxEnemySpawns,
            respawnMinTicks: RespawnMinTicks, respawnMaxTicks: RespawnMaxTicks);
        public void Validate()
        {
            if (float.IsNaN(TickDelta) || float.IsInfinity(TickDelta) || TickDelta <= 0 || TickDelta > 1)
                throw new ArgumentException("Tick delta must be finite and in (0,1].");
            if (MaxTicks < 1 || MaxTicks > 100000 || MaxInputs < 1 || MaxInputs > 100000 || TraceCapacity < 1 || TraceCapacity > 65536)
                throw new ArgumentException("Run budgets are outside supported bounds.");
            if (EnemyHealthMax == int.MaxValue || RespawnMaxTicks == int.MaxValue || MaxEnemySpawns > 4096)
                throw new ArgumentException("Random ranges and spawn budget exceed this adapter's supported bounds.");
            CreateRules();
        }
    }
}
