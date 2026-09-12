using Module.SystemFacts;

namespace Module.SystemFacts.Observability
{
    public interface ISystemFactSink
    {
        void Publish(ISystemFact fact);
    }
}