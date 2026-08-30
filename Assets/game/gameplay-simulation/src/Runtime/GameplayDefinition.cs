using System;
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
        public override string PolicyId => "gameplay-template-v1/splitmix64/lifecycle-v3";
        protected override void ValidateScenario(GameplayScenario scenario) => scenario.Validate();
        protected override float GetTickDelta(GameplayScenario scenario) => scenario.TickDelta;
        protected override GameplayWorld CreateWorld(GameplayScenario scenario) => new GameplayWorld(scenario);
        protected override void DestroyWorld(GameplayWorld world) { } // Managed, session-owned objects only.
        protected override void ConfigureWorld(SimulationBuilder builder, GameplayWorld world, GameplayScenario scenario) => world.Configure(builder);
        protected override InputOutcome ExecuteInput(GameplayWorld world, GameplayInput input, IDomainEventSink events) => world.Execute(input, events);
        protected override GameplayObservation CaptureObservation(GameplayWorld world) => world.Observe();
        protected override void ConfigureInvariants(InvariantRegistry<GameplayObservation> invariants) => invariants.Register(new GameplayInvariant());
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
