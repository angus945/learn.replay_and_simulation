using System;
using DeterministicSimulation.Framework;
using Module.Verification.StateSnapshot;

namespace Arena.Integration
{
    public sealed class ArenaObservationAdapter : ISimulationObserver<ArenaRuntime, ArenaObservation>
    {
        private readonly StateSnapshotChannel<ArenaObservation> channel;

        public ArenaObservationAdapter(int capacity = 128)
        {
            channel = new StateSnapshotChannel<ArenaObservation>(capacity);
        }

        public IStateSnapshotReader<ArenaObservation> Reader
        {
            get { return channel.ReaderPort; }
        }

        public ArenaObservation Observe(ArenaRuntime world)
        {
            return new ArenaObservation(world);
        }

        public StateSnapshotReference Publish(SimulationSession<ArenaRuntime, ArenaScenario> session, string sessionId, long epoch)
        {
            ArenaObservation observation = session.Observe(this);
            StateSnapshotCaptureMetadata metadata = new StateSnapshotCaptureMetadata("arena.runtime", sessionId, epoch, "tick:" + observation.Tick);
            return channel.PublisherPort.Publish(observation, metadata);
        }

        public void ReportFailure(string sessionId, long epoch, string code, string detail)
        {
            StateSnapshotCaptureMetadata metadata = new StateSnapshotCaptureMetadata("arena.runtime", sessionId, epoch);
            channel.PublisherPort.ReportStateSnapshotCaptureFailure(new StateSnapshotCaptureFailure(metadata, code, detail));
        }
    }
}
