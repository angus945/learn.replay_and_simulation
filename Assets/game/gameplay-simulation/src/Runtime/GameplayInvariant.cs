using InvariantChecks;
using System.Collections.Generic;

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
}
