using Module.SystemFacts;

namespace Module.SystemFacts.Observability
{
    public sealed class ObservedFact
    {
        public ObservedFact(ISystemFact fact, ObservationContext context)
        {
            Fact = fact;
            Context = context;
        }

        public ISystemFact Fact { get; }

        public ObservationContext Context { get; }
    }
    public sealed class ObservedFact<TFact> where TFact : ISystemFact
    {
        public ObservedFact(TFact fact, ObservationContext context)
        {
            Fact = fact;
            Context = context;
        }

        public TFact Fact { get; }

        public ObservationContext Context { get; }
    }

}