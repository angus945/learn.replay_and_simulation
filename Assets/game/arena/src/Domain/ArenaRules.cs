using System;

namespace Arena.Domain
{
    /// <summary>Immutable gameplay policy. Tick duration, trace limits and recording budgets do not belong here.</summary>
    public sealed class ArenaRules
    {
        public ArenaRules(
            float speed = 4f,
            int playerHealth = 30,
            int enemyHealthMin = 20,
            int enemyHealthMax = 40,
            int damage = 10,
            float attackRange = 2f,
            int maxEnemySpawns = 12,
            int respawnMinTicks = 30,
            int respawnMaxTicks = 90)
        {
            if (!Position.IsFinite(speed) || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (playerHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerHealth));
            if (enemyHealthMin <= 0 || enemyHealthMax < enemyHealthMin)
                throw new ArgumentOutOfRangeException(nameof(enemyHealthMin));
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (!Position.IsFinite(attackRange) || attackRange < 0f)
                throw new ArgumentOutOfRangeException(nameof(attackRange));
            if (maxEnemySpawns < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEnemySpawns));
            if (respawnMinTicks < 0 || respawnMaxTicks < respawnMinTicks)
                throw new ArgumentOutOfRangeException(nameof(respawnMinTicks));

            Speed = speed;
            PlayerHealth = playerHealth;
            EnemyHealthMin = enemyHealthMin;
            EnemyHealthMax = enemyHealthMax;
            Damage = damage;
            AttackRange = attackRange;
            MaxEnemySpawns = maxEnemySpawns;
            RespawnMinTicks = respawnMinTicks;
            RespawnMaxTicks = respawnMaxTicks;
        }

        public float Speed { get; }
        public int PlayerHealth { get; }
        public int EnemyHealthMin { get; }
        public int EnemyHealthMax { get; }
        public int Damage { get; }
        public float AttackRange { get; }
        public int MaxEnemySpawns { get; }
        public int RespawnMinTicks { get; }
        public int RespawnMaxTicks { get; }
    }
}
