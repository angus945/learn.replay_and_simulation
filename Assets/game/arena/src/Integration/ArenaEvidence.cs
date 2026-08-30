using System;
using System.IO;
using System.Text;
using InvariantChecks;
using Testability;

namespace Arena.Integration
{
    public static class ArenaCodecs
    {
        public static string Encode<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            { ArtifactJson.Write(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public static T Decode<T>(string payload) where T : class
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
                return ArtifactJson.Read<T>(stream) ?? throw new ArgumentException("Null arena payload.");
        }
    }
    public static class ArenaCanonicalState
    {
        public static byte[] Encode(ArenaObservation observation)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(observation.Tick); writer.Write(observation.PlayerId);
                    WriteFloat(writer, observation.TickDelta); WriteFloat(writer, observation.Rules.Speed);
                    writer.Write(observation.Rules.PlayerHealth); writer.Write(observation.Rules.EnemyHealthMin);
                    writer.Write(observation.Rules.EnemyHealthMax); writer.Write(observation.Rules.Damage);
                    WriteFloat(writer, observation.Rules.AttackRange); writer.Write(observation.Rules.MaxEnemySpawns);
                    writer.Write(observation.Rules.RespawnMinTicks); writer.Write(observation.Rules.RespawnMaxTicks);
                    writer.Write(observation.LastActorId); writer.Write(observation.EnemiesSpawned);
                    writer.Write(observation.HealthRandomState); writer.Write(observation.DelayRandomState);
                    writer.Write(observation.PendingRespawnTicks.Count);
                    foreach (ulong tick in observation.PendingRespawnTicks) writer.Write(tick);
                    writer.Write(observation.RegistryEvidence.Count);
                    foreach (ulong value in observation.RegistryEvidence) writer.Write(value);
                    writer.Write(observation.Actors.Count);
                    foreach (ActorSnapshot actor in observation.Actors)
                    {
                        writer.Write(actor.Id); writer.Write(actor.Enemy); WriteFloat(writer, actor.X); WriteFloat(writer, actor.Y);
                        WriteFloat(writer, actor.DirectionX); WriteFloat(writer, actor.DirectionY); WriteFloat(writer, actor.Speed);
                        writer.Write(actor.Health); writer.Write(actor.MaxHealth);
                    }
                }
                return stream.ToArray();
            }
        }
        private static void WriteFloat(BinaryWriter writer, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Non-finite canonical state.");
            writer.Write(value == 0 ? 0f : value);
        }
    }
    public sealed class ArenaInvariant : IInvariant<ArenaObservation>
    {
        public string Code => "arena.committed-state";
        public InvariantViolation Evaluate(ArenaObservation state)
        {
            if (state.RegistryActiveCount != state.Actors.Count) return new InvariantViolation(Code, "Registry/repository disagreement.");
            ulong previous = 0;
            foreach (ActorSnapshot actor in state.Actors)
            {
                if (actor.Id <= previous || actor.Health <= 0 || actor.Health > actor.MaxHealth)
                    return new InvariantViolation(Code, "Unordered identity or dead actor survived commit.");
                if (!Domain.Position.IsFinite(actor.X) || !Domain.Position.IsFinite(actor.Y) ||
                    !Domain.Position.IsFinite(actor.DirectionX) || !Domain.Position.IsFinite(actor.DirectionY) ||
                    (double)actor.DirectionX * actor.DirectionX + (double)actor.DirectionY * actor.DirectionY > 1.000001)
                    return new InvariantViolation(Code, "Invalid movement snapshot.");
                previous = actor.Id;
            }
            return null;
        }
    }
    /// <summary>Opt-in teaching oracle, never enabled by the normal gameplay composition.</summary>
    public sealed class TrainingPositionOracle : IInvariant<ArenaObservation>
    {
        public string Code => "tutorial.position-limit";
        public InvariantViolation Evaluate(ArenaObservation state)
        {
            ActorSnapshot player = state.FindActor(state.PlayerId);
            return player != null && player.X > 1.5f ? new InvariantViolation(Code, "Training oracle: player X exceeded 1.5.") : null;
        }
    }
}
