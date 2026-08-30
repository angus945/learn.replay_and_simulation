using System;
using System.Collections.Generic;
using NUnit.Framework;
using WaveDispatching;

namespace WaveDispatching.Tests
{
    public sealed class WaveDispatcherTests
    {
        [Test]
        public void DispatchAll_DefersReentrantItemsToNextWave()
        {
            WaveDispatcher<int> dispatcher = new WaveDispatcher<int>();
            List<string> trace = new List<string>();
            dispatcher.Enqueue(1);

            dispatcher.DispatchAll((wave, value) =>
            {
                trace.Add($"{wave}:{value}");
                if (value == 1)
                {
                    dispatcher.Enqueue(2);
                }
            });

            CollectionAssert.AreEqual(new[] { "0:1", "1:2" }, trace);
            Assert.That(dispatcher.HasPending, Is.False);
        }

        [Test]
        public void DispatchAll_WhenWaveLimitIsExceeded_ClearsPendingWork()
        {
            WaveDispatcher<int> dispatcher = new WaveDispatcher<int>(1);
            dispatcher.Enqueue(1);

            Assert.Throws<InvalidOperationException>(() =>
                dispatcher.DispatchAll((_, value) => dispatcher.Enqueue(value + 1)));

            Assert.That(dispatcher.HasPending, Is.False);
        }
    }
}
