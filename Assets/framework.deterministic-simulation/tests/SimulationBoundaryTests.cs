using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class SimulationBoundaryTests
    {
        [Test]
        public void MissingAndDuplicateIntentHandlersFailExplicitly()
        {
            SimulationPipeline missing = new SimulationPipeline();
            missing.Seal(); missing.EnqueueIntent(new Intent());
            Assert.Throws<InvalidOperationException>(() => new SimulationRunner(missing).AdvanceTick());
            SimulationPipeline configured = new SimulationPipeline();
            configured.RegisterIntentHandler(new Handler());
            Assert.Throws<InvalidOperationException>(() => configured.RegisterIntentHandler(new Handler()));
            configured.Seal();
            Assert.Throws<InvalidOperationException>(() => configured.RegisterIntentHandler(new Handler()));
        }

        [Test]
        public void DispatchObserverReportsActualCategoryAndWave()
        {
            List<MessageDispatch> trace = new List<MessageDispatch>();
            SimulationPipeline pipeline = new SimulationPipeline(onDispatch: trace.Add);
            pipeline.RegisterIntentHandler(new Handler());
            pipeline.Seal(); pipeline.EnqueueIntent(new Intent());
            new SimulationRunner(pipeline).AdvanceTick();
            Assert.That(trace.Count, Is.EqualTo(1));
            Assert.That(trace[0].Category, Is.EqualTo(MessageCategory.Intent));
            Assert.That(trace[0].Wave, Is.GreaterThanOrEqualTo(0));
            Assert.That(trace[0].Message, Is.TypeOf<Intent>());
        }

        private readonly struct Intent : IIntent { }
        private sealed class Handler : IIntentHandler<Intent>
        {
            public void Handle(Intent intent) { }
        }
    }
}
