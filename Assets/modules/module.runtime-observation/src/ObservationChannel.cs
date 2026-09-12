using System;
using System.Collections.Generic;

namespace RuntimeObservation
{
    public sealed class ObservationChannel<TObservation>
    {
        private sealed class Entry
        {
            public Entry(ObservationReference reference, TObservation observation, CaptureMetadata metadata, CaptureFailure failure)
            {
                Reference = reference;
                Observation = observation;
                Metadata = metadata;
                Failure = failure;
            }

            public ObservationReference Reference { get; }
            public TObservation Observation { get; }
            public CaptureMetadata Metadata { get; }
            public CaptureFailure Failure { get; }
        }

        private sealed class Reader : IObservationReader<TObservation>
        {
            private readonly ObservationChannel<TObservation> owner;

            public Reader(ObservationChannel<TObservation> owner)
            {
                this.owner = owner;
            }

            public ObservationRead<TObservation> ReadLatest()
            {
                return owner.ReadLatest();
            }

            public ObservationRead<TObservation> Read(ObservationReference reference)
            {
                return owner.Read(reference);
            }
        }

        private sealed class Publisher : IObservationPublisher<TObservation>
        {
            private readonly ObservationChannel<TObservation> owner;

            public Publisher(ObservationChannel<TObservation> owner)
            {
                this.owner = owner;
            }

            public ObservationReference Publish(TObservation observation, CaptureMetadata metadata)
            {
                return owner.Publish(observation, metadata);
            }

            public void ReportCaptureFailure(CaptureFailure failure)
            {
                owner.ReportCaptureFailure(failure);
            }
        }

        private readonly object gate = new object();
        private readonly Guid channelId = Guid.NewGuid();
        private readonly Queue<Entry> retained = new Queue<Entry>();
        private readonly Dictionary<long, Entry> byCaptureId = new Dictionary<long, Entry>();
        private long nextCaptureId;

        public ObservationChannel(int capacity = 64)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            ReaderPort = new Reader(this);
            PublisherPort = new Publisher(this);
        }

        public int Capacity { get; }
        public IObservationReader<TObservation> ReaderPort { get; }
        public IObservationPublisher<TObservation> PublisherPort { get; }

        private ObservationReference Publish(TObservation observation, CaptureMetadata metadata)
        {
            if (ReferenceEquals(observation, null)) throw new ArgumentNullException(nameof(observation));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            lock (gate)
            {
                ObservationReference reference = NextReference(metadata);
                Retain(new Entry(reference, observation, metadata, null));
                return reference;
            }
        }

        private void ReportCaptureFailure(CaptureFailure failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            lock (gate)
            {
                ObservationReference reference = NextReference(failure.Metadata);
                Retain(new Entry(reference, default(TObservation), failure.Metadata, failure));
            }
        }

        private ObservationReference NextReference(CaptureMetadata metadata)
        {
            nextCaptureId = checked(nextCaptureId + 1);
            return new ObservationReference(channelId, nextCaptureId, metadata.SourceId, metadata.ScopeId, metadata.Epoch);
        }

        private void Retain(Entry entry)
        {
            if (retained.Count == Capacity)
            {
                Entry removed = retained.Dequeue();
                byCaptureId.Remove(removed.Reference.CaptureId);
            }
            retained.Enqueue(entry);
            byCaptureId.Add(entry.Reference.CaptureId, entry);
        }

        private ObservationRead<TObservation> ReadLatest()
        {
            lock (gate)
            {
                if (retained.Count == 0) return Empty(ObservationReadState.Empty);
                Entry[] entries = retained.ToArray();
                return ToRead(entries[entries.Length - 1]);
            }
        }

        private ObservationRead<TObservation> Read(ObservationReference reference)
        {
            lock (gate)
            {
                if (reference.ChannelId != channelId) return Empty(ObservationReadState.ForeignReference);
                Entry entry;
                if (byCaptureId.TryGetValue(reference.CaptureId, out entry))
                {
                    if (entry.Reference != reference) return Empty(ObservationReadState.ForeignReference);
                    return ToRead(entry);
                }
                if (reference.CaptureId <= nextCaptureId) return Empty(ObservationReadState.Evicted);
                return Empty(ObservationReadState.Empty);
            }
        }

        private static ObservationRead<TObservation> ToRead(Entry entry)
        {
            ObservationReadState state = entry.Failure == null ? ObservationReadState.Available : ObservationReadState.CaptureFailed;
            return new ObservationRead<TObservation>(state, entry.Reference, entry.Observation, entry.Metadata, entry.Failure);
        }

        private static ObservationRead<TObservation> Empty(ObservationReadState state)
        {
            return new ObservationRead<TObservation>(state, default(ObservationReference), default(TObservation), null, null);
        }
    }
}
