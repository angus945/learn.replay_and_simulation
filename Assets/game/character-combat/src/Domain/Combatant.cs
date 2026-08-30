using System;

namespace CharacterCombat
{
    /// <summary>Health aggregate. Simulation identity/lifecycle adapters live outside this domain.</summary>
    public sealed class Combatant
    {
        public Combatant(int maxHealth)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth = maxHealth;
            Health = maxHealth;
        }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public bool IsDead => Health == 0;
        public int TakeDamage(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            int applied = Math.Min(Health, damage);
            Health -= applied;
            return applied;
        }
    }
}
