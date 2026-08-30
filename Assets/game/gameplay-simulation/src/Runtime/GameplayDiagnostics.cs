using System;
using Invariants;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Testability;

namespace GameplaySimulation
{
    public sealed class GameplayInvariant : IInvariant<GameplayObservation>
    {
        public string Code => "gameplay.valid_state";
        public InvariantViolation Evaluate(GameplayObservation observation)
        {
            HashSet<ulong> ids = new HashSet<ulong>();
            foreach (ActorObservation actor in observation.Actors)
            {
                if (actor.Id == 0 || !ids.Add(actor.Id)) return new InvariantViolation("actor.id_unique", "Duplicate or zero actor identity.");
                if (actor.MaxHealth <= 0 || actor.Health < 0 || actor.Health > actor.MaxHealth)
                    return new InvariantViolation("health.bounds", "Health outside legal range: " + actor.Id);
                if (!GameplayScenario.Finite(actor.X) || !GameplayScenario.Finite(actor.Y) ||
                    !GameplayScenario.Finite(actor.DirectionX) || !GameplayScenario.Finite(actor.DirectionY) ||
                    !GameplayScenario.Finite(actor.Speed) || actor.Speed < 0)
                    return new InvariantViolation("movement.finite", "Invalid movement state: " + actor.Id);
                if ((double)actor.DirectionX * actor.DirectionX + (double)actor.DirectionY * actor.DirectionY > 1.000001)
                    return new InvariantViolation("movement.unit_direction", "Direction exceeds unit disk: " + actor.Id);
                if (actor.Active != (actor.Health > 0)) return new InvariantViolation("lifecycle.committed", "Health and registry lifetime disagree: " + actor.Id);
                if (actor.Health == 0 && (actor.DirectionX != 0 || actor.DirectionY != 0))
                    return new InvariantViolation("dead.stationary", "Dead actor has movement intent: " + actor.Id);
            }
            return null;
        }
    }

    public static class GameplayStateHasher
    {
        public static string Compute(GameplayObservation state, GameplayScenario scenario)
        {
            using (MemoryStream bytes = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write(1); // Canonical gameplay schema version; BinaryWriter uses little endian.
                    writer.Write(state.Tick); WriteFloat(writer, scenario.TickDelta); WriteFloat(writer, scenario.Speed);
                    writer.Write(scenario.Health); writer.Write(scenario.Damage); WriteFloat(writer, scenario.AttackRange);
                    writer.Write(scenario.IncludeEnemy); writer.Write(scenario.Seed);
                    List<ActorObservation> actors = new List<ActorObservation>(state.Actors);
                    actors.Sort((left, right) => left.Id.CompareTo(right.Id));
                    writer.Write(actors.Count);
                    foreach (ActorObservation actor in actors)
                    {
                        writer.Write(actor.Id); WriteFloat(writer, actor.X); WriteFloat(writer, actor.Y);
                        WriteFloat(writer, actor.DirectionX); WriteFloat(writer, actor.DirectionY); WriteFloat(writer, actor.Speed);
                        writer.Write(actor.Health); writer.Write(actor.MaxHealth); writer.Write(actor.Active);
                    }
                }
                return StateDigest.Compute(bytes.ToArray());
            }
        }
        private static void WriteFloat(BinaryWriter writer, float value)
        {
            if (!GameplayScenario.Finite(value)) throw new InvalidOperationException("Cannot hash non-finite gameplay state.");
            writer.Write(value == 0 ? 0f : value); // Canonicalize negative zero. Exact float bits, same-runtime guarantee only.
        }
    }

    /// <summary>Bounded in-process regression helper, not Phase 5 exploration or universal replay.</summary>
    public static class ScenarioRerun
    {
        public static bool VerifyFailure(FailureArtifact artifact, GameplaySession freshSession = null)
        {
            if (artifact == null || artifact.SchemaVersion != 1) throw new ArgumentException("Unsupported failure artifact schema.");
            return FailureRerun.Compare(artifact, freshSession).Matches;
        }

        public static IReadOnlyList<TickReport> Run(GameplayScenario scenario, IEnumerable<GameplayRequest> actions,
            int ticks, GameplaySession session = null)
        {
            if (ticks < 0 || ticks > scenario.MaxTicks) throw new ArgumentOutOfRangeException(nameof(ticks));
            GameplaySession target = session ?? new GameplaySession();
            target.Admin.Start(scenario);
            foreach (GameplayRequest action in actions)
            {
                SubmissionResult submission = target.Gameplay.Submit(action.InSession(target.Id));
                if (!submission.Queued) throw new InvalidOperationException(submission.Code);
            }
            List<TickReport> reports = new List<TickReport>();
            for (int i = 0; i < ticks && target.State == SessionState.Running; i++) reports.Add(target.Simulation.Step());
            return reports.AsReadOnly();
        }
    }
}
