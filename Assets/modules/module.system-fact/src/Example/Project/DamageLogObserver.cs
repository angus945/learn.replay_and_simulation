using System;
using Module.SystemFacts.Observability;

namespace Module.SystemFacts.Example
{
    public sealed class DamageLogObserver : ISystemFactObserver<DamageApplied>
    {
        public void Observe(DamageApplied fact, ObservationContext context)
        {
            Console.WriteLine($"[{context.Sequence}] DamageApplied Character={fact.CharacterId} HP={fact.PreviousHp}->{fact.CurrentHp} Damage={fact.Damage}");
        }
    }
}