using System;
using System.Collections.Generic;
using CharacterCombat;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace GameplaySimulation
{
    /// <summary>Project-owned composition of the movement and health models, not a framework entity.</summary>
    internal sealed class GameplayActor
    {
        internal GameplayActor(ulong id, bool isEnemy, MovementAggregate movement, Combatant combat)
        { Id = id; IsEnemy = isEnemy; Movement = movement; Combat = combat; }
        internal ulong Id { get; }
        internal bool IsEnemy { get; }
        internal MovementAggregate Movement { get; }
        internal Combatant Combat { get; }
    }

    internal enum GameplayDecision { Accepted, Rejected, InvalidRequest }

    internal readonly struct GameplayOutcome
    {
        internal GameplayOutcome(GameplayDecision decision, string code, int damage = 0, bool died = false)
        { Decision = decision; Code = code; Damage = damage; Died = died; }
        internal GameplayDecision Decision { get; }
        internal string Code { get; }
        internal int Damage { get; }
        internal bool Died { get; }
    }

    /// <summary>Gameplay use cases. No simulation phases, message sinks, Unity or testability types.
    /// The caller maps the returned facts to integration events after the domain decision.</summary>
    internal sealed class GameplayActions
    {
        private readonly IReadOnlyDictionary<ulong, GameplayActor> actors;
        private readonly MovementApplication movement;
        private readonly GameplayScenario rules;

        internal GameplayActions(IReadOnlyDictionary<ulong, GameplayActor> actors,
            MovementApplication movement, GameplayScenario rules)
        { this.actors = actors; this.movement = movement; this.rules = rules; }

        internal GameplayOutcome Execute(GameplayInput input)
        {
            if (input == null || !Enum.IsDefined(typeof(GameplayActionKind), input.Kind))
                return new GameplayOutcome(GameplayDecision.InvalidRequest, "action.unknown");
            if (input.Actor == 0 || !GameplayScenario.Finite(input.X) || !GameplayScenario.Finite(input.Y))
                return new GameplayOutcome(GameplayDecision.InvalidRequest, "parameters.invalid");
            if (!actors.TryGetValue(input.Actor, out GameplayActor actor))
                return new GameplayOutcome(GameplayDecision.Rejected, "actor.unknown");
            if (actor.Combat.IsDead)
                return new GameplayOutcome(GameplayDecision.Rejected, "actor.dead");
            if (input.Kind == GameplayActionKind.Move)
            {
                movement.TrySetDirection(actor.Movement.Id, MovementDirection.FromAxes(input.X, input.Y));
                return new GameplayOutcome(GameplayDecision.Accepted, "move.applied");
            }
            if (input.Target == input.Actor)
                return new GameplayOutcome(GameplayDecision.Rejected, "target.self");
            if (!actors.TryGetValue(input.Target, out GameplayActor target))
                return new GameplayOutcome(GameplayDecision.Rejected, "target.unknown");
            if (target.Combat.IsDead)
                return new GameplayOutcome(GameplayDecision.Rejected, "target.dead");
            double dx = (double)actor.Movement.Position.X - target.Movement.Position.X;
            double dy = (double)actor.Movement.Position.Y - target.Movement.Position.Y;
            if (dx * dx + dy * dy > (double)rules.AttackRange * rules.AttackRange)
                return new GameplayOutcome(GameplayDecision.Rejected, "target.out_of_range");
            int applied = target.Combat.TakeDamage(rules.Damage);
            if (target.Combat.IsDead) target.Movement.SetDesiredDirection(default);
            return new GameplayOutcome(GameplayDecision.Accepted, "attack.applied", applied, target.Combat.IsDead);
        }

        internal void Advance(float seconds)
        {
            // The composition supplies actors in stable ID order. Dead actors wait for structural commit.
            foreach (GameplayActor actor in actors.Values)
                if (!actor.Combat.IsDead) actor.Movement.Advance(seconds);
        }
    }
}
