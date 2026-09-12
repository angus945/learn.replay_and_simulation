using Module.SystemFacts;

namespace Module.SystemFacts.Observability
{
    public interface ISystemFactObserver
    {
        void Observe(ObservedFact observedFact);
    }
    public interface ISystemFactObserver<in TFact> where TFact : ISystemFact
    {
        void Observe(TFact fact, ObservationContext context);
    }
}