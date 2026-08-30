using System.Collections.Generic;

namespace Testability
{
    public interface IStateObserver<out T> { T Observe(); }
    public interface ITestSession<in TScenario>
    {
        string Id { get; }
        SessionState State { get; }
        void Start(TScenario scenario);
        void Reset(TScenario scenario);
        void Stop();
    }
}
