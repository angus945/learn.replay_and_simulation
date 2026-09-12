using Module.SystemFacts.Observability;
using Module.SystemFacts;
using System.Collections.Generic;
using System;

namespace Module.SystemFacts.Observability
{
    public sealed class SystemFactHub : ISystemFactSink
    {
        private readonly Dictionary<Type, List<Action<ISystemFact, ObservationContext>>> _routes = new Dictionary<Type, List<Action<ISystemFact, ObservationContext>>>();

        private long _sequence;

        public void Register<TFact>(ISystemFactObserver<TFact> observer) where TFact : ISystemFact
        {
            Type factType = typeof(TFact);

            if (!_routes.TryGetValue(factType, out List<Action<ISystemFact, ObservationContext>> observers))
            {
                observers = new List<Action<ISystemFact, ObservationContext>>();
                _routes.Add(factType, observers);
            }

            observers.Add((fact, context) => observer.Observe((TFact)fact, context));
        }

        public void Publish(ISystemFact fact)
        {
            Type factType = fact.GetType();

            if (!_routes.TryGetValue(factType, out List<Action<ISystemFact, ObservationContext>> observers))
            {
                return;
            }

            _sequence++;

            ObservationContext context = new ObservationContext(_sequence, DateTimeOffset.UtcNow);

            foreach (Action<ISystemFact, ObservationContext> observer in observers)
            {
                try
                {
                    observer(fact, context);
                }
                catch
                {
                    // Observer failure must not affect application flow.
                }
            }
        }
    }
}