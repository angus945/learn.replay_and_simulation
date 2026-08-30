using System;
using DeterministicSimulation;

namespace DeterministicSimulation.Framework.Tests
{
    // Executed both by NUnit and the pure .NET guide runner; no Unity dependency.
    public static class SessionTemplateContractChecks
    {
        public sealed class Counter
        {
            public int Value { get; private set; }
            public void Increment() => Value++;
        }

        private readonly struct IncrementIntent : IIntent { }
        private readonly struct MissingCommand : IInternalCommand { }
        private sealed class CounterAdapter : IIntentHandler<IncrementIntent>
        {
            private readonly Counter counter;
            private readonly Action callback;
            internal CounterAdapter(Counter counter, Action callback) { this.counter = counter; this.callback = callback; }
            public void Handle(IncrementIntent intent) { counter.Increment(); callback?.Invoke(); }
        }
        private sealed class CounterObserver : ISimulationObserver<Counter, int>
        {
            public int Observe(Counter world) => world.Value;
        }

        private sealed class CounterDefinition : SimulationDefinition<Counter, float>
        {
            internal int Destroyed;
            internal bool Missing;
            internal bool FailCleanup;
            internal Action Callback;
            internal SimulationBuilder CapturedBuilder;
            protected override void ValidateScenario(float scenario) { }
            protected override float GetTickDelta(float scenario) => scenario;
            protected override Counter CreateWorld(float scenario) => new Counter();
            protected override void Configure(SimulationBuilder builder, Counter world, float scenario)
            {
                CapturedBuilder = builder;
                builder.RequireIntent<IncrementIntent>();
                if (Missing) builder.RequireCommand<MissingCommand>();
                else builder.RegisterIntentHandler(new CounterAdapter(world, Callback));
            }
            protected override void DestroyWorld(Counter world)
            {
                Destroyed++;
                if (FailCleanup) throw new InvalidOperationException("cleanup");
            }
        }

        public static void Lifecycle()
        {
            CounterDefinition definition = new CounterDefinition();
            CounterObserver observer = new CounterObserver();
            SimulationSession<Counter, float> session = definition.CreateSession(.25f);
            session.EnqueueIntent(new IncrementIntent());
            Check(session.Observe(observer) == 0, "Admission must not execute.");
            session.Step();
            Check(session.Observe(observer) == 1 && session.LastCompletedTick == 1, "Step failed.");
            Expect<InvalidOperationException>(() => definition.CapturedBuilder.RequireIntent<IncrementIntent>());
            session.EnqueueIntent(new IncrementIntent());
            session.Stop();
            Expect<InvalidOperationException>(() => session.Step());
            Check(session.Observe(observer) == 1, "Stop must preserve observation.");
            session.Reset(.25f);
            Check(definition.Destroyed == 1 && session.TickNumber == 0, "Reset must rebuild.");
            session.Step();
            Check(session.Observe(observer) == 0, "Reset retained old queued intent.");
            session.Dispose(); session.Dispose();
            Check(definition.Destroyed == 2, "Cleanup must happen exactly once per world.");
            Expect<ObjectDisposedException>(() => session.Step());
        }

        public static void MissingConfiguration()
        {
            CounterDefinition definition = new CounterDefinition { Missing = true };
            InvalidOperationException error = Expect<InvalidOperationException>(() => definition.CreateSession(.25f));
            Check(error.Message.Contains("IncrementIntent") && error.Message.Contains("MissingCommand"), "Report all declared missing handlers.");
            Check(definition.Destroyed == 1, "Failed setup leaked its world.");
            definition.FailCleanup = true;
            AggregateException combined = Expect<AggregateException>(() => definition.CreateSession(.25f));
            Check(combined.InnerExceptions.Count == 2, "Preserve setup and cleanup errors.");
        }

        public static void FaultAndReentry()
        {
            CounterDefinition definition = new CounterDefinition();
            SimulationSession<Counter, float> session = null;
            definition.Callback = () => session.Reset(.25f);
            session = definition.CreateSession(.25f);
            session.Step();
            session.EnqueueIntent(new IncrementIntent());
            InvalidOperationException failure = Expect<InvalidOperationException>(() => session.Step());
            Check(session.State == SimulationSessionState.Faulted && session.TickNumber == 2 && session.LastCompletedTick == 1, "Fault tick attribution.");
            Check(ReferenceEquals(session.Failure, failure), "Retain first failure.");
            Check(session.Observe(new CounterObserver()) == 1, "No rollback should be claimed.");
            session.Stop();
            Expect<InvalidOperationException>(() => session.Step());
            Check(ReferenceEquals(session.Failure, failure), "Stop replaced fault.");
            definition.Callback = null;
            session.Reset(.25f);
            Check(session.Failure == null && session.State == SimulationSessionState.Running, "Reset recovery.");
            session.Dispose();
        }

        public static void ResetFailures()
        {
            CounterDefinition definition = new CounterDefinition();
            SimulationSession<Counter, float> session = definition.CreateSession(.25f);
            Expect<ArgumentOutOfRangeException>(() => session.Reset(float.NaN));
            Check(definition.Destroyed == 0 && session.State == SimulationSessionState.Running, "Invalid scenario destroyed old world.");
            definition.Missing = true;
            Expect<InvalidOperationException>(() => session.Reset(.25f));
            Check(definition.Destroyed == 2 && session.State == SimulationSessionState.Faulted, "Reset setup cleanup.");
            Expect<InvalidOperationException>(() => session.Observe(new CounterObserver()));
            definition.Missing = false;
            session.Reset(.25f);
            definition.FailCleanup = true;
            Expect<InvalidOperationException>(() => session.Dispose());
            session.Dispose();
            Check(definition.Destroyed == 3 && session.State == SimulationSessionState.Disposed, "Do not repeat failing cleanup.");
        }

        public static void IndependentSessions()
        {
            CounterDefinition definition = new CounterDefinition();
            using (SimulationSession<Counter, float> first = definition.CreateSession(.25f))
            using (SimulationSession<Counter, float> second = definition.CreateSession(.25f))
            {
                first.EnqueueIntent(new IncrementIntent()); first.Step();
                Check(second.Observe(new CounterObserver()) == 0 && second.TickNumber == 0, "Worlds are shared.");
            }
        }

        private static T Expect<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("Expected " + typeof(T).Name);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); }
    }
}
