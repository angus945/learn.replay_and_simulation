using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using InvariantChecks;
using Testability;
using Testability.Templates;

namespace GameplaySimulation
{
    /// <summary>Concrete integration template. Domain aggregates remain framework-independent.</summary>
    public sealed class GameplayDefinition : ReplayableSimulationDefinition<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation>
    {
        public const string DefaultPolicy = "gameplay-template-v1/splitmix64/lifecycle-v3";
        private readonly string policy;
        private readonly IReadOnlyList<Func<IInvariant<GameplayObservation>>> additionalInvariants;
        public GameplayDefinition() : this(null, DefaultPolicy) { }
        public GameplayDefinition(IEnumerable<Func<IInvariant<GameplayObservation>>> additionalInvariants,
            string policyId = DefaultPolicy)
        {
            if (string.IsNullOrWhiteSpace(policyId)) throw new ArgumentException("A replay policy identity is required.", nameof(policyId));
            List<Func<IInvariant<GameplayObservation>>> factories = new List<Func<IInvariant<GameplayObservation>>>(
                additionalInvariants ?? Array.Empty<Func<IInvariant<GameplayObservation>>>());
            foreach (Func<IInvariant<GameplayObservation>> factory in factories)
                if (factory == null) throw new ArgumentException("Invariant factories cannot be null.", nameof(additionalInvariants));
            if (factories.Count > 0 && policyId == DefaultPolicy)
                throw new ArgumentException("Custom invariants require an explicit policyId.", nameof(policyId));
            this.additionalInvariants = factories.AsReadOnly();
            policy = policyId;
        }
        public override string PolicyId => policy;
        protected override TemplateLimits CreateDefaultLimits(GameplayScenario scenario) =>
            new TemplateLimits(scenario.MaxTicks, scenario.MaxActions, scenario.TraceCapacity, maxTotalPayloadBytes: 8388608);
        protected override void ValidateScenario(GameplayScenario scenario) => scenario.Validate();
        protected override float GetTickDelta(GameplayScenario scenario) => scenario.TickDelta;
        protected override GameplayWorld CreateWorld(GameplayScenario scenario) => new GameplayWorld(scenario);
        protected override void DestroyWorld(GameplayWorld world) { } // Managed, session-owned objects only.
        protected override void ConfigureWorld(SimulationBuilder builder, GameplayWorld world, GameplayScenario scenario) => world.Configure(builder);
        protected override InputOutcome ExecuteInput(GameplayWorld world, GameplayInput input, InputExecutionContext context) =>
            world.Execute(input, context.Events, context.Sequence);
        protected override GameplayObservation CaptureObservation(GameplayWorld world) => world.Observe();
        protected override void ConfigureInvariants(InvariantRegistry<GameplayObservation> invariants)
        {
            invariants.Register(new GameplayInvariant());
            foreach (Func<IInvariant<GameplayObservation>> factory in additionalInvariants) invariants.Register(factory());
        }
        protected override TemplateTraceMetadata DescribeInput(GameplayInput input) =>
            new TemplateTraceMetadata(input.Kind.ToString(), actor: input.Actor, target: input.Target);
        protected override TemplateTraceMetadata DescribeMessage(object message)
        {
            if (message is GameplayWorld.ActorDamaged damage)
                return new TemplateTraceMetadata("ActorDamaged", damage.Sequence, damage.Actor, damage.Target,
                    "damage=" + damage.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (message is GameplayWorld.ActorDied death)
                return new TemplateTraceMetadata("ActorDied", death.Sequence, death.Actor, death.Target);
            if (message is GameplayWorld.LifecycleNotice notice)
                return new TemplateTraceMetadata(notice.Type, actor: notice.Actor, detail: notice.Code);
            return null;
        }
        protected override string EncodeScenario(GameplayScenario scenario) => Encode(scenario);
        protected override GameplayScenario DecodeScenario(string payload) => Decode<GameplayScenario>(payload);
        protected override string EncodeInput(GameplayInput input) => Encode(input ?? throw new ArgumentNullException(nameof(input)));
        protected override GameplayInput DecodeInput(string payload) => Decode<GameplayInput>(payload);
        private static string Encode<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            { ArtifactJson.Write(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        private static T Decode<T>(string payload) where T : class
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
                return ArtifactJson.Read<T>(stream) ?? throw new ArgumentException("Null gameplay payload.");
        }
        protected override byte[] EncodeCanonicalState(GameplayObservation observation)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(observation.Tick);
                    writer.Write(observation.EnemyRandomState); writer.Write(observation.RespawnRandomState);
                    writer.Write(observation.EnemiesSpawned); writer.Write(observation.PendingRespawnTicks.Count);
                    foreach (ulong tick in observation.PendingRespawnTicks) writer.Write(tick);
                    writer.Write(observation.Actors.Count); // World emits stable ID order.
                    foreach (ActorObservation actor in observation.Actors)
                    {
                        writer.Write(actor.Id); WriteFloat(writer, actor.X); WriteFloat(writer, actor.Y);
                        WriteFloat(writer, actor.DirectionX); WriteFloat(writer, actor.DirectionY); WriteFloat(writer, actor.Speed);
                        writer.Write(actor.Health); writer.Write(actor.MaxHealth); writer.Write(actor.Active);
                    }
                }
                return stream.ToArray();
            }
        }
        private static void WriteFloat(BinaryWriter writer, float value)
        {
            if (!GameplayScenario.Finite(value)) throw new InvalidOperationException("Non-finite gameplay state.");
            writer.Write(value == 0 ? 0f : value);
        }
    }
}
