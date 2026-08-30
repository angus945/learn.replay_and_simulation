using System;
using NUnit.Framework;

namespace CharacterCombat.Tests
{
    public sealed class CombatantTests
    {
        [Test]
        public void InvalidHealthConfigurationIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Combatant(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Combatant(-1));
        }

        [Test]
        public void ZeroDamagePreservesHealthAndZeroHealthIsTerminal()
        {
            Combatant actor = new Combatant(10);
            Assert.That(actor.TakeDamage(0), Is.Zero);
            Assert.That(actor.Health, Is.EqualTo(10));
            Assert.That(actor.TakeDamage(20), Is.EqualTo(10));
            Assert.That(actor.TakeDamage(1), Is.Zero);
            Assert.That(actor.IsDead, Is.True);
        }
    }
}
