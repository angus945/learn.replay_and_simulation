using System.Collections.Generic;

namespace Module.SystemFacts
{
    public interface ISystemFactSource
    {
        IReadOnlyCollection<ISystemFact> ReleaseFacts();
    }
}