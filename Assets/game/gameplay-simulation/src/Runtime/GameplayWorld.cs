using System;
using System.Collections.Generic;
using CharacterCombat;
using CharacterMovement.Application;
using CharacterMovement.Domain;
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
        internal readonly struct ActorDamaged : IDomainEvent
        {
            internal ActorDamaged(ulong sequence, ulong actor, ulong target, int damage)
            { Sequence = sequence; Actor = actor; Target = target; Damage = damage; }
            internal ulong Sequence { get; }
            internal ulong Actor { get; }
            internal ulong Target { get; }
            internal int Damage { get; }
        }
        internal readonly struct ActorDied : IDomainEvent
        {
            internal ActorDied(ulong sequence, ulong actor, ulong target)
            { Sequence = sequence; Actor = actor; Target = target; }
            internal ulong Sequence { get; }
            internal ulong Actor { get; }
            internal ulong Target { get; }
        }
        internal readonly struct SpawnEnemy : IInternalCommand { }
        internal readonly struct LifecycleNotice : IDomainEvent
        {
            internal LifecycleNotice(string type, string code, ulong actor = 0)
            { Type = type; Code = code; Actor = actor; }
            internal string Type { get; }
            internal string Code { get; }
            internal ulong Actor { get; }
        }
        private SplitMix64Random enemyRandom;
        private SplitMix64Random respawnRandom;
        private readonly List<ulong> pendingRespawnTicks = new List<ulong>();
        private int enemiesSpawned;
        private int pendingEnemySpawns;
        private readonly GameplayScenario scenario;
        private readonly SimulationObjectRegistry objects = new SimulationObjectRegistry();
        private readonly CharacterMovementRepository movements = new CharacterMovementRepository();
        private readonly GameplayActions actions;
        private readonly SortedDictionary<ulong, GameplayActor> actors = new SortedDictionary<ulong, GameplayActor>();
        private IInternalCommandSink commands;
        private IDomainEventSink events;
        public ulong CurrentTick { get; private set; }
        public ulong PlayerId { get; }
        internal GameplayWorld(GameplayScenario scenario)
        {
            this.scenario = scenario;
            actions = new GameplayActions(actors, new MovementApplication(movements), scenario);
            enemyRandom = SplitMix64Random.FromStream(scenario.Seed, 1);
            respawnRandom = SplitMix64Random.FromStream(scenario.Seed, 2);
            PlayerId = Spawn(default, false);
            if (scenario.IncludeEnemy) Spawn(new MovementPosition(1, 0), true);
            objects.Commit();
        }
        internal void Configure(SimulationBuilder builder)
        {
            commands = builder.Commands;
            events = builder.Events;
            builder.RegisterInternalCommandHandler<SpawnEnemy>(this);
            builder.RegisterDomainEventHandler<ActorDied>(this);
            builder.RegisterPrePhysicsParticipant(this);
            builder.RegisterStructuralCommitParticipant(this);
        }
        private ulong Spawn(MovementPosition position, bool enemy)
        {
            SimulationObjectRecord identity = objects.RequestSpawn();
            MovementAggregate movement = new MovementAggregate(new CharacterId(identity.Id.Value), position, scenario.Speed);
            movements.Add(movement);
            int health = enemy && scenario.RandomEnemyHealth ? enemyRandom.NextInt(scenario.EnemyHealthMin, scenario.EnemyHealthMax + 1) : scenario.Health;
            if (enemy) enemiesSpawned++;
            actors.Add(identity.Id.Value, new GameplayActor(identity.Id.Value, enemy, movement, new Combatant(health)));
            return identity.Id.Value;
        }

        public GameplayObservation Observe()
        {
            List<ActorObservation> snapshot = new List<ActorObservation>();
            foreach (KeyValuePair<ulong, GameplayActor> pair in actors)
            {
                GameplayActor actor = pair.Value;
                bool active = objects.TryGet(new SimulationObjectId(actor.Id), out SimulationObjectRecord record) && record.IsActive;
                snapshot.Add(new ActorObservation(pair.Key, actor.Movement.Position.X, actor.Movement.Position.Y,
                    actor.Movement.DesiredDirection.X, actor.Movement.DesiredDirection.Y, actor.Movement.Speed,
                    actor.Combat.Health, actor.Combat.MaxHealth, active));
            }
            return new GameplayObservation(CurrentTick, snapshot, enemyRandom == null ? 0 : enemyRandom.CaptureState().Value, enemiesSpawned,
                respawnRandom == null ? 0 : respawnRandom.CaptureState().Value, pendingRespawnTicks,
                playerId: PlayerId, lifecycle: new LifecycleSnapshot(objects.GetActiveOrdered().Count,
                    movements.GetActiveOrdered().Count, actors.Count, enemiesSpawned, pendingEnemySpawns + pendingRespawnTicks.Count));
        }

        internal InputOutcome Execute(GameplayInput request, IDomainEventSink events, ulong sequence)
        {
            GameplayOutcome outcome = actions.Execute(request);
            if (outcome.Damage > 0)
                events.PublishDomainEvent(new ActorDamaged(sequence, request.Actor, request.Target, outcome.Damage));
            if (outcome.Died)
                events.PublishDomainEvent(new ActorDied(sequence, request.Actor, request.Target));
            ActionStatus status = outcome.Decision == GameplayDecision.Accepted ? ActionStatus.Accepted
                : outcome.Decision == GameplayDecision.Rejected ? ActionStatus.Rejected : ActionStatus.InvalidRequest;
            return new InputOutcome(status, outcome.Code);
        }

        void IDomainEventHandler<ActorDied>.Handle(ActorDied death)
        {
            GameplayActor actor = actors[death.Target];
            if (!objects.TryGet(new SimulationObjectId(actor.Id), out SimulationObjectRecord identity))
                throw new InvalidOperationException("Dying actor has no simulation identity.");
            bool requested = objects.RequestDestroy(identity.Handle);
            if (requested && actor.IsEnemy && scenario.RespawnEnemies) commands.EnqueueInternalCommand(new SpawnEnemy());
        }
        void IInternalCommandHandler<SpawnEnemy>.Handle(SpawnEnemy command) { pendingEnemySpawns++; }

        void IPrePhysicsParticipant.Tick(SimulationContext context)
        {
            CurrentTick = context.Tick.Number;
            actions.Advance(context.Tick.DeltaTime);
        }

        void IStructuralCommitParticipant.Commit(SimulationContext context)
        {
            StructuralCommitResult result = objects.Commit();
            foreach (SimulationObjectRecord destroyed in result.Destroyed)
            {
                movements.Remove(actors[destroyed.Id.Value].Movement.Id);
                events.PublishDomainEvent(new LifecycleNotice("Destroyed", "destroy.committed", destroyed.Id.Value));
            }
            while (pendingEnemySpawns > 0)
            {
                pendingEnemySpawns--;
                if (enemiesSpawned + pendingRespawnTicks.Count >= scenario.MaxEnemySpawns)
                { events.PublishDomainEvent(new LifecycleNotice("SpawnSkipped", "spawn.budget")); continue; }
                if (scenario.RandomRespawnDelay)
                {
                    int minTicks = (int)Math.Ceiling(1d / scenario.TickDelta);
                    int maxTicks = (int)Math.Floor(3d / scenario.TickDelta);
                    ulong due = checked(CurrentTick + (ulong)respawnRandom.NextInt(minTicks, maxTicks + 1));
                    pendingRespawnTicks.Add(due);
                    pendingRespawnTicks.Sort();
                    events.PublishDomainEvent(new LifecycleNotice("RespawnScheduled", "respawn.scheduled"));
                }
                else events.PublishDomainEvent(new LifecycleNotice("Spawned", "spawn.committed", Spawn(new MovementPosition(1, 0), true)));
            }
            while (pendingRespawnTicks.Count > 0 && pendingRespawnTicks[0] <= CurrentTick)
            {
                pendingRespawnTicks.RemoveAt(0);
                events.PublishDomainEvent(new LifecycleNotice("Spawned", "spawn.committed", Spawn(new MovementPosition(1, 0), true)));
            }
            objects.Commit();
            ValidateLifecycle();

        }

        private void ValidateLifecycle()
        {
            int active = 0;
            foreach (GameplayActor actor in actors.Values)
            {
                bool registered = objects.TryGet(new SimulationObjectId(actor.Id), out SimulationObjectRecord record) && record.IsActive;
                bool inRepository = movements.TryGet(actor.Movement.Id, out MovementAggregate ignored);
                if (registered != inRepository || registered == actor.Combat.IsDead) throw new InvalidOperationException("Registry/repository/domain lifetime disagreement.");
                if (registered) active++;
            }
            if (active != objects.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned registry object.");
            if (active != movements.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned movement repository object.");
        }

    }
}
