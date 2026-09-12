using System;
using System.Collections.Generic;
using Module.Verification.SystemFact;
using Module.Verification.SystemFact.Observability;
using NUnit.Framework;

namespace Module.Verification.SystemFact.Tests
{
    public sealed class SystemFactHubTests
    {
        private sealed class ExecutionFact : IExecutionFact
        {
        }

        private sealed class DomainFact : IDomainFact
        {
        }

        private sealed class CollectingObserver<TFact> : ISystemFactObserver<TFact> where TFact : ISystemFact
        {
            private readonly List<long> sequences = new List<long>();

            public IReadOnlyList<long> Sequences
            {
                get { return sequences.AsReadOnly(); }
            }

            public void Observe(TFact fact, FactObservationContext context)
            {
                sequences.Add(context.Sequence);
            }
        }

        private sealed class ThrowingObserver : ISystemFactObserver<IExecutionFact>
        {
            public void Observe(IExecutionFact fact, FactObservationContext context)
            {
                throw new InvalidOperationException("observer failed");
            }
        }

        private sealed class CollectingFailureSink : IObservationFailureSink
        {
            private readonly List<ObservationFailure> failures = new List<ObservationFailure>();

            public IReadOnlyList<ObservationFailure> Failures
            {
                get { return failures.AsReadOnly(); }
            }

            public void Report(ObservationFailure failure)
            {
                failures.Add(failure);
            }
        }

        private sealed class ThrowingFailureSink : IObservationFailureSink
        {
            public void Report(ObservationFailure failure)
            {
                throw new InvalidOperationException("failure sink failed");
            }
        }

        [Test]
        public void AssignableRoutesReceiveConcreteFactsAndPublishSequenceIncludesUnobservedFacts()
        {
            CollectingObserver<IExecutionFact> observer = new CollectingObserver<IExecutionFact>();
            SystemFactHubBuilder builder = new SystemFactHubBuilder();
            builder.Register<IExecutionFact>(observer);
            SystemFactHub hub = builder.Build();

            hub.Publish(new DomainFact());
            hub.Publish(new ExecutionFact());

            Assert.That(observer.Sequences, Is.EqualTo(new long[] { 2 }));
        }

        [Test]
        public void NullFactFailsDeterministically()
        {
            SystemFactHub hub = new SystemFactHubBuilder().Build();
            ArgumentNullException captured = null;

            try
            {
                hub.Publish(null);
            }
            catch (ArgumentNullException exception)
            {
                captured = exception;
            }

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.ParamName, Is.EqualTo("fact"));
        }

        [Test]
        public void ObserverFailuresAreReportedWithoutStoppingOtherObservers()
        {
            CollectingFailureSink failureSink = new CollectingFailureSink();
            CollectingObserver<IExecutionFact> observer = new CollectingObserver<IExecutionFact>();
            SystemFactHubBuilder builder = new SystemFactHubBuilder();
            builder.ReportFailuresTo(failureSink);
            builder.Register<IExecutionFact>(new ThrowingObserver());
            builder.Register<IExecutionFact>(observer);
            SystemFactHub hub = builder.Build();

            hub.Publish(new ExecutionFact());

            Assert.That(failureSink.Failures.Count, Is.EqualTo(1));
            Assert.That(observer.Sequences, Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void FailureSinkFailuresDoNotAffectPublishing()
        {
            CollectingObserver<IExecutionFact> observer = new CollectingObserver<IExecutionFact>();
            SystemFactHubBuilder builder = new SystemFactHubBuilder();
            builder.ReportFailuresTo(new ThrowingFailureSink());
            builder.Register<IExecutionFact>(new ThrowingObserver());
            builder.Register<IExecutionFact>(observer);
            SystemFactHub hub = builder.Build();

            hub.Publish(new ExecutionFact());

            Assert.That(observer.Sequences, Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void BuilderFreezesAfterBuild()
        {
            SystemFactHubBuilder builder = new SystemFactHubBuilder();
            builder.Build();
            InvalidOperationException captured = null;

            try
            {
                builder.Register<IExecutionFact>(new CollectingObserver<IExecutionFact>());
            }
            catch (InvalidOperationException exception)
            {
                captured = exception;
            }

            Assert.That(captured, Is.Not.Null);
        }
    }
}
