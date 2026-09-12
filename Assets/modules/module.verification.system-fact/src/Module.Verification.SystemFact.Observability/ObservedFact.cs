using Module.Verification.SystemFact;

namespace Module.Verification.SystemFact.Observability
{
    public sealed class ObservedFact
    {
        public ObservedFact(ISystemFact fact, FactObservationContext context)
        {
            Fact = fact;
            Context = context;
        }

        public ISystemFact Fact { get; }

        public FactObservationContext Context { get; }
    }
    public sealed class ObservedFact<TFact> where TFact : ISystemFact
    {
        public ObservedFact(TFact fact, FactObservationContext context)
        {
            Fact = fact;
            Context = context;
        }

        public TFact Fact { get; }

        public FactObservationContext Context { get; }
    }

}
