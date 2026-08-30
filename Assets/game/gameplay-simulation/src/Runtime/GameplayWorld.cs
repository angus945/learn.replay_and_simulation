using System;
using System.Collections.Generic;
using CharacterCombat;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using SimulationObjects;
using SimulationObjects.Contract;
using Testability;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;
using SeededRandom;

using Testability.Templates;

namespace GameplaySimulation
{
    /// <summary>Project-owned domain composition; the framework owns input, ticks and evidence.</summary>
    public sealed class GameplayWorld : IDomainEventHandler<GameplayWorld.ActorDied>, IInternalCommandHandler<GameplayWorld.SpawnEnemy>, IPrePhysicsParticipant, IStructuralCommitParticipant
    {
        private sealed class Actor
        {
            internal MovementAggregate Movement;
            internal Combatant Combat;
            internal SimulationObjectRecord Identity;
        }
        internal readonly struct ActorDamaged : IDomainEvent
        {
            internal ActorDamaged(ulong target, int damage) { Target = target; Damage = damage; }
            internal ulong Target { get; }
            internal int Damage { get; }
        }
        internal readonly struct ActorDied : IDomainEvent
        {
            internal ActorDied(ulong target) { Target = target; }
            internal ulong Target { get; }
        }
        internal readonly struct SpawnEnemy : IInternalCommand { }
        private SplitMix64Random enemyRandom;
        private SplitMix64Random respawnRandom;
        private readonly List<ulong> pendingRespawnTicks = new List<ulong>();
        private int enemiesSpawned;
        private int pendingEnemySpawns;
        private readonly GameplayScenario scenario;
        private readonly SimulationObjectRegistry objects = new SimulationObjectRegistry();
        private readonly CharacterMovementRepository movements = new CharacterMovementRepository();
        private readonly MovementApplication movementApplication;
        private readonly SortedDictionary<ulong, Actor> actors = new SortedDictionary<ulong, Actor>();
        private IInternalCommandSink commands;
        public ulong CurrentTick { get; private set; }
        internal GameplayWorld(GameplayScenario scenario)
        {
            this.scenario = scenario;
            movementApplication = new MovementApplication(movements);
            enemyRandom = SplitMix64Random.FromStream(scenario.Seed, 1);
            respawnRandom = SplitMix64Random.FromStream(scenario.Seed, 2);
            Spawn(default, false);
            if (scenario.IncludeEnemy) Spawn(new MovementPosition(1, 0), true);
            objects.Commit();
        }
        internal void Configure(SimulationBuilder builder)
        {
            commands = builder.Commands;
            builder.RegisterInternalCommandHandler<SpawnEnemy>(this);
            builder.RegisterDomainEventHandler<ActorDied>(this);
            builder.RegisterPrePhysicsParticipant(this);
            builder.RegisterStructuralCommitParticipant(this);
        }
        private void Spawn(MovementPosition position, bool enemy)
        {
            SimulationObjectRecord identity = objects.RequestSpawn();
            MovementAggregate movement = new MovementAggregate(new CharacterId(identity.Id.Value), position, scenario.Speed);
            movements.Add(movement);
            int health = enemy && scenario.RandomEnemyHealth ? enemyRandom.NextInt(scenario.EnemyHealthMin, scenario.EnemyHealthMax + 1) : scenario.Health;
            if (enemy) enemiesSpawned++;
            actors.Add(identity.Id.Value, new Actor { Identity = identity, Movement = movement, Combat = new Combatant(health) });
        }

        public GameplayObservation Observe()
        {
            List<ActorObservation> snapshot = new List<ActorObservation>();
            foreach (KeyValuePair<ulong, Actor> pair in actors)
            {
                Actor actor = pair.Value;
                bool active = objects.TryGet(actor.Identity.Handle, out SimulationObjectRecord record) && record.IsActive;
                snapshot.Add(new ActorObservation(pair.Key, actor.Movement.Position.X, actor.Movement.Position.Y,
                    actor.Movement.DesiredDirection.X, actor.Movement.DesiredDirection.Y, actor.Movement.Speed,
                    actor.Combat.Health, actor.Combat.MaxHealth, active));
            }
            return new GameplayObservation(CurrentTick, snapshot, enemyRandom == null ? 0 : enemyRandom.CaptureState().Value, enemiesSpawned,
                respawnRandom == null ? 0 : respawnRandom.CaptureState().Value, pendingRespawnTicks);
        }

        internal InputOutcome Execute(GameplayInput request, IDomainEventSink events)
        {
            if (!Enum.IsDefined(typeof(GameplayActionKind), request.Kind)) { return new InputOutcome(ActionStatus.InvalidRequest, "action.unknown"); }
            if (request.Actor == 0 || !GameplayScenario.Finite(request.X) || !GameplayScenario.Finite(request.Y))
            { return new InputOutcome(ActionStatus.InvalidRequest, "parameters.invalid"); }
            if (!actors.TryGetValue(request.Actor, out Actor actor)) { return new InputOutcome(ActionStatus.Rejected, "actor.unknown"); }
            if (actor.Combat.IsDead) { return new InputOutcome(ActionStatus.Rejected, "actor.dead"); }
            if (request.Kind == GameplayActionKind.Move)
            {
                PlayerMoveIntent move = new PlayerMoveIntent(actor.Movement.Id, MovementDirection.FromAxes(request.X, request.Y));
                movementApplication.TrySetDirection(move.Character, move.Direction);
                return new InputOutcome(ActionStatus.Accepted, "move.applied");
            }
            if (request.Target == request.Actor) { return new InputOutcome(ActionStatus.Rejected, "target.self"); }
            if (!actors.TryGetValue(request.Target, out Actor target)) { return new InputOutcome(ActionStatus.Rejected, "target.unknown"); }
            if (target.Combat.IsDead) { return new InputOutcome(ActionStatus.Rejected, "target.dead"); }
            double dx = (double)actor.Movement.Position.X - target.Movement.Position.X;
            double dy = (double)actor.Movement.Position.Y - target.Movement.Position.Y;
            if (dx * dx + dy * dy > (double)scenario.AttackRange * scenario.AttackRange)
            { return new InputOutcome(ActionStatus.Rejected, "target.out_of_range"); }
            int applied = target.Combat.TakeDamage(scenario.Damage);
            events.PublishDomainEvent(new ActorDamaged(request.Target, applied));
            if (target.Combat.IsDead)
            {
                target.Movement.SetDesiredDirection(default);
                events.PublishDomainEvent(new ActorDied(request.Target));
            }
            return new InputOutcome(ActionStatus.Accepted, "attack.applied");
        }

        void IDomainEventHandler<ActorDied>.Handle(ActorDied death)
        {
            bool requested = objects.RequestDestroy(actors[death.Target].Identity.Handle);
            if (requested && death.Target != 1 && scenario.RespawnEnemies) commands.EnqueueInternalCommand(new SpawnEnemy());
        }
        void IInternalCommandHandler<SpawnEnemy>.Handle(SpawnEnemy command) { pendingEnemySpawns++; }

        void IPrePhysicsParticipant.Tick(SimulationContext context)
        {
            CurrentTick = context.Tick.Number;
            foreach (Actor actor in actors.Values)
                if (!actor.Combat.IsDead) actor.Movement.Advance(context.Tick.DeltaTime);
        }

        void IStructuralCommitParticipant.Commit(SimulationContext context)
        {
            StructuralCommitResult result = objects.Commit();
            foreach (SimulationObjectRecord destroyed in result.Destroyed)
            {
                movements.Remove(new CharacterId(destroyed.Id.Value));

            }
            while (pendingEnemySpawns > 0)
            {
                pendingEnemySpawns--;
                if (enemiesSpawned + pendingRespawnTicks.Count >= scenario.MaxEnemySpawns)
                { continue; }
                if (scenario.RandomRespawnDelay)
                {
                    int minTicks = (int)Math.Ceiling(1d / scenario.TickDelta);
                    int maxTicks = (int)Math.Floor(3d / scenario.TickDelta);
                    ulong due = checked(CurrentTick + (ulong)respawnRandom.NextInt(minTicks, maxTicks + 1));
                    pendingRespawnTicks.Add(due);
                    pendingRespawnTicks.Sort();

                }
                else Spawn(new MovementPosition(1, 0), true);
            }
            while (pendingRespawnTicks.Count > 0 && pendingRespawnTicks[0] <= CurrentTick)
            {
                pendingRespawnTicks.RemoveAt(0);
                Spawn(new MovementPosition(1, 0), true);
            }
            objects.Commit();
            ValidateLifecycle();

        }

        private void ValidateLifecycle()
        {
            int active = 0;
            foreach (Actor actor in actors.Values)
            {
                bool registered = objects.TryGet(actor.Identity.Handle, out SimulationObjectRecord record) && record.IsActive;
                bool inRepository = movements.TryGet(actor.Movement.Id, out MovementAggregate ignored);
                if (registered != inRepository || registered == actor.Combat.IsDead) throw new InvalidOperationException("Registry/repository/domain lifetime disagreement.");
                if (registered) active++;
            }
            if (active != objects.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned registry object.");
            if (active != movements.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned movement repository object.");
        }

    }
}
