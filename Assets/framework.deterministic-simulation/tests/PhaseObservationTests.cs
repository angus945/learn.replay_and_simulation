using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class PhaseObservationTests
    {
        private sealed class Participant : IIntentSource, IPrePhysicsParticipant, IPhysicsParticipant, IPostPhysicsParticipant, IStructuralCommitParticipant, IPresentationParticipant
        {
            internal SimulationPhase Fail;
            private void Check(SimulationContext context) { if (context.Phase == Fail) throw new InvalidOperationException("injected"); }
            public void AcquireIntents(SimulationContext context, IIntentSink sink) => Check(context);
            public void Tick(SimulationContext context) => Check(context);
            public void Simulate(SimulationContext context) => Check(context);
            public void Commit(SimulationContext context) => Check(context);
            public void CaptureTickState(SimulationContext context) => Check(context);
            public void Render(SimulationContext context, float alpha) { }
        }
        [TestCase(SimulationPhase.IntentAcquisition)]
        [TestCase(SimulationPhase.PrePhysics)]
        [TestCase(SimulationPhase.Physics)]
        [TestCase(SimulationPhase.PostPhysics)]
        [TestCase(SimulationPhase.StructuralCommit)]
        [TestCase(SimulationPhase.PresentationCapture)]
        public void FailedPhaseHasBeginButNoEndAndNoLaterPhase(SimulationPhase fail)
        {
            List<string> records = new List<string>();
            SimulationPipeline pipeline = new SimulationPipeline(onPhase: (phase, begin) => records.Add(phase + ":" + begin));
            Participant participant = new Participant { Fail = fail };
            pipeline.RegisterIntentSource(participant); pipeline.RegisterPrePhysicsParticipant(participant);
            pipeline.RegisterPhysicsParticipant(participant); pipeline.RegisterPostPhysicsParticipant(participant);
            pipeline.RegisterStructuralCommitParticipant(participant); pipeline.RegisterPresentationParticipant(participant); pipeline.Seal();
            SimulationRunner runner = new SimulationRunner(pipeline);
            Assert.Throws<InvalidOperationException>(() => runner.AdvanceTick());
            Assert.That(records[records.Count - 1], Is.EqualTo(fail + ":True"));
            Assert.That(runner.TickNumber, Is.EqualTo(1));
        }
        [Test]
        public void EmptyPipelineStillReportsEveryAuthoritativePhaseInOrder()
        {
            List<string> records = new List<string>();
            SimulationPipeline pipeline = new SimulationPipeline(onPhase: (phase, begin) => records.Add(phase + ":" + begin)); pipeline.Seal();
            new SimulationRunner(pipeline).AdvanceTick();
            Assert.That(records.Count, Is.EqualTo(14));
            Assert.That(records[0], Is.EqualTo("IntentAcquisition:True"));
            Assert.That(records[records.Count - 1], Is.EqualTo("PresentationCapture:False"));
        }
    }
}
