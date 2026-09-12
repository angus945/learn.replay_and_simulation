using System;
using DeterministicSimulation.Framework;
using RuntimeObservation;

namespace Arena.Integration
{
    public sealed class ArenaObservationAdapter : ISimulationObserver<ArenaRuntime, ArenaObservation>
    {
        private readonly ObservationChannel<ArenaObservation> channel;

        public ArenaObservationAdapter(int capacity = 128)
        {
            channel = new ObservationChannel<ArenaObservation>(capacity);
        }

        public IObservationReader<ArenaObservation> Reader
        {
            get { return channel.ReaderPort; }
        }

        public ArenaObservation Observe(ArenaRuntime world)
        {
            return new ArenaObservation(world);
        }

        public ObservationReference Publish(SimulationSession<ArenaRuntime, ArenaScenario> session, string sessionId, long epoch)
        {
            ArenaObservation observation = session.Observe(this);
            CaptureMetadata metadata = new CaptureMetadata("arena.runtime", sessionId, epoch, "tick:" + observation.Tick);
            return channel.PublisherPort.Publish(observation, metadata);
        }

        public void ReportFailure(string sessionId, long epoch, string code, string detail)
        {
            CaptureMetadata metadata = new CaptureMetadata("arena.runtime", sessionId, epoch);
            channel.PublisherPort.ReportCaptureFailure(new CaptureFailure(metadata, code, detail));
        }
    }
}
