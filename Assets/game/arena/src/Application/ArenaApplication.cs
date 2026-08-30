using System;
using System.Collections.Generic;
using Arena.Domain;

namespace Arena.Application
{
    /// <summary>
    /// Coordinates arena use cases. The outer layer decides when these operations run,
    /// while this layer owns game identities, spawn budgets and respawn due ticks.
    /// </summary>
    public sealed class ArenaApplication
    {
        private readonly IActorRepository repository;
        private readonly IActorLifecycle lifecycle;
        private readonly ISpawnRandom random;
        private readonly List<ulong> pendingRespawnTicks = new List<ulong>();

        public ArenaApplication(IActorRepository repository, IActorLifecycle lifecycle, ISpawnRandom random, ArenaRules rules)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (repository.ReadOrdered().Count != 0)
                throw new ArgumentException("Each arena session requires an empty repository.", nameof(repository));

            PlayerId = NextActorId();
            lifecycle.Spawn(new Actor(PlayerId, ActorKind.Player, new Position(0f, 0f), Rules.Speed, Rules.PlayerHealth));
            SpawnEnemy();
            lifecycle.Commit();
        }

        public ActorId PlayerId { get; }
        public ulong Tick { get; private set; }
        public ulong LastActorId { get; private set; }
        public int EnemiesSpawned { get; private set; }
        public ArenaRules Rules { get; }
        public IReadOnlyList<Actor> Actors => repository.ReadOrdered();

        // The caller cannot mutate the live queue, and a retained observation never changes later.
        public IReadOnlyList<ulong> PendingRespawnTicks => Array.AsReadOnly(pendingRespawnTicks.ToArray());

        public ArenaResult Execute(ArenaRequest request)
        {
            if (request.Kind != ArenaAction.Move && request.Kind != ArenaAction.Attack)
                return new ArenaResult(ArenaDecision.InvalidRequest, "invalid-action");
            if (!request.Actor.IsValid)
                return new ArenaResult(ArenaDecision.InvalidRequest, "invalid-actor");
            if (!Position.IsFinite(request.X) || !Position.IsFinite(request.Y))
                return new ArenaResult(ArenaDecision.InvalidRequest, "invalid-direction");
            if (!repository.TryGet(request.Actor, out Actor actor))
                return new ArenaResult(ArenaDecision.Rejected, "actor-not-found");
            if (actor.IsDead)
                return new ArenaResult(ArenaDecision.Rejected, "actor-dead");

            if (request.Kind == ArenaAction.Move)
            {
                actor.SetDirection(request.X, request.Y);
                return new ArenaResult(ArenaDecision.Accepted, "moved");
            }

            if (!request.Target.IsValid)
                return new ArenaResult(ArenaDecision.InvalidRequest, "invalid-target");
            if (request.Actor == request.Target)
                return new ArenaResult(ArenaDecision.Rejected, "self-target");
            if (!repository.TryGet(request.Target, out Actor target))
                return new ArenaResult(ArenaDecision.Rejected, "target-not-found");
            if (target.IsDead)
                return new ArenaResult(ArenaDecision.Rejected, "target-dead");

            double x = (double)actor.Position.X - target.Position.X;
            double y = (double)actor.Position.Y - target.Position.Y;
            double rangeSquared = (double)Rules.AttackRange * Rules.AttackRange;
            if (x * x + y * y > rangeSquared)
                return new ArenaResult(ArenaDecision.Rejected, "out-of-range");

            int damage = target.TakeDamage(Rules.Damage);
            ArenaFact damaged = new ArenaFact(ArenaFactKind.Damaged, actor.Id, target.Id, damage);
            if (!target.IsDead)
                return new ArenaResult(ArenaDecision.Accepted, "damaged", damaged);

            ArenaFact defeated = new ArenaFact(ArenaFactKind.Defeated, actor.Id, target.Id, damage);
            return new ArenaResult(ArenaDecision.Accepted, "defeated", damaged, defeated);
        }

        public void Advance(ulong tick, float seconds)
        {
            if (tick <= Tick)
                throw new ArgumentOutOfRangeException(nameof(tick), "An arena tick must advance monotonically.");
            if (!Position.IsFinite(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            IReadOnlyList<Actor> actors = repository.ReadOrdered();
            for (int index = 0; index < actors.Count; index++)
                actors[index].Advance(seconds);
            Tick = tick;
        }

        public bool OnDefeated(ActorId id)
        {
            if (repository.TryGet(id, out Actor actor) && actor.IsDead)
            {
                lifecycle.Despawn(id);
                return actor.Kind == ActorKind.Enemy;
            }
            return false;
        }

        public bool ScheduleRespawn(ulong tick)
        {
            // Reservations count against the total budget before any random draw.
            if (EnemiesSpawned >= Rules.MaxEnemySpawns - pendingRespawnTicks.Count)
                return false;
            if (tick > ulong.MaxValue - (ulong)Rules.RespawnMaxTicks)
                throw new ArgumentOutOfRangeException(nameof(tick), "Respawn due tick would overflow.");

            int delay = random.NextDelay(Rules.RespawnMinTicks, Rules.RespawnMaxTicks);
            if (delay < Rules.RespawnMinTicks || delay > Rules.RespawnMaxTicks)
                throw new InvalidOperationException("The random adapter returned a delay outside the requested range.");

            ulong dueTick = tick + (ulong)delay;
            int insertion = pendingRespawnTicks.BinarySearch(dueTick);
            if (insertion < 0)
                insertion = ~insertion;
            pendingRespawnTicks.Insert(insertion, dueTick);
            return true;
        }

        public void Commit(ulong tick)
        {
            lifecycle.Commit();
            int dueCount = 0;
            while (dueCount < pendingRespawnTicks.Count && pendingRespawnTicks[dueCount] <= tick)
            {
                SpawnEnemy();
                dueCount++;
            }

            if (dueCount > 0)
                pendingRespawnTicks.RemoveRange(0, dueCount);
            lifecycle.Commit();
        }

        private ActorId NextActorId()
        {
            if (LastActorId == ulong.MaxValue)
                throw new InvalidOperationException("Arena actor identities are exhausted.");
            LastActorId++;
            return new ActorId(LastActorId);
        }

        private void SpawnEnemy()
        {
            int health = random.NextHealth(Rules.EnemyHealthMin, Rules.EnemyHealthMax);
            if (health < Rules.EnemyHealthMin || health > Rules.EnemyHealthMax)
                throw new InvalidOperationException("The random adapter returned health outside the requested range.");

            Actor enemy = new Actor(NextActorId(), ActorKind.Enemy, new Position(1f, 0f), Rules.Speed, health);
            lifecycle.Spawn(enemy);
            EnemiesSpawned++;
        }
    }
}
