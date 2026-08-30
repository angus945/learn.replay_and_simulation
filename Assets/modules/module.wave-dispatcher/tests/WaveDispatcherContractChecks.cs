using System;
using System.Collections.Generic;

namespace WaveDispatching.Tests
{
    /// <summary>Pure C# checks shared by NUnit and the headless contract runner.</summary>
    public static class WaveDispatcherContractChecks
    {
        public static void CallbackGuardsPreserveQueuedItems()
        {
            WaveDispatcher<int> dispatcher = new WaveDispatcher<int>();
            List<string> observed = new List<string>();
            dispatcher.Enqueue(1);
            dispatcher.Enqueue(2);
            dispatcher.DispatchAll((wave, item) =>
            {
                observed.Add(wave + ":" + item);
                if (item != 1) return;
                dispatcher.Enqueue(3);
                Expect<InvalidOperationException>(() => dispatcher.DispatchAll((nestedWave, nestedItem) => { }));
                Expect<InvalidOperationException>(() => dispatcher.Clear());
            });
            Check(string.Join(",", observed) == "0:1,0:2,1:3", "Nested dispatch or Clear lost the rest of the current wave.");
            Check(!dispatcher.HasPending, "Completed dispatch retained pending work.");
        }

        public static void CallbackFailureClearsWorkAndReleasesGuard()
        {
            WaveDispatcher<int> dispatcher = new WaveDispatcher<int>();
            dispatcher.Enqueue(1);
            dispatcher.Enqueue(2);
            Expect<InvalidOperationException>(() => dispatcher.DispatchAll((wave, item) =>
            {
                dispatcher.Enqueue(3);
                dispatcher.Clear();
            }));
            Check(!dispatcher.HasPending, "Failed dispatch retained pending work.");
            dispatcher.Enqueue(4);
            int received = 0;
            dispatcher.DispatchAll((wave, item) => received = item);
            Check(received == 4, "Failure left the dispatcher guard locked or leaked failed work.");
            dispatcher.Enqueue(5);
            dispatcher.Clear();
            Check(!dispatcher.HasPending, "Clear between dispatch calls must remain supported.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Expect<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("Expected " + typeof(TException).Name);
        }
    }
}
