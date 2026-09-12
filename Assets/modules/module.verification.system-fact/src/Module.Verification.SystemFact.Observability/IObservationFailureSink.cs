using System;
using Module.Verification.SystemFact;

namespace Module.Verification.SystemFact.Observability
{
    public sealed class ObservationFailure
    {
        public ObservationFailure(ISystemFact fact, FactObservationContext context, Type observerType, Exception exception)
        {
            Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ObserverType = observerType ?? throw new ArgumentNullException(nameof(observerType));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public ISystemFact Fact { get; }
        public FactObservationContext Context { get; }
        public Type ObserverType { get; }
        public Exception Exception { get; }
    }

    public interface IObservationFailureSink
    {
        void Report(ObservationFailure failure);
    }

    internal sealed class NullObservationFailureSink : IObservationFailureSink
    {
        public static readonly NullObservationFailureSink Instance = new NullObservationFailureSink();

        private NullObservationFailureSink()
        {
        }

        public void Report(ObservationFailure failure)
        {
        }
    }
}
