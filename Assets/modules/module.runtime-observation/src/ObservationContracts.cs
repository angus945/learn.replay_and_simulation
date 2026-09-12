using System;

namespace RuntimeObservation
{
    public sealed class CaptureMetadata
    {
        public CaptureMetadata(string sourceId, string scopeId, long epoch, string correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source identity is required.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("A scope identity is required.", nameof(scopeId));
            if (epoch < 0) throw new ArgumentOutOfRangeException(nameof(epoch));
            SourceId = sourceId;
            ScopeId = scopeId;
            Epoch = epoch;
            CorrelationId = correlationId;
        }

        public string SourceId { get; }
        public string ScopeId { get; }
        public long Epoch { get; }
        public string CorrelationId { get; }
    }

    public readonly struct ObservationReference : IEquatable<ObservationReference>
    {
        public ObservationReference(Guid channelId, long captureId, string sourceId, string scopeId, long epoch)
        {
            if (channelId == Guid.Empty) throw new ArgumentException("A channel identity is required.", nameof(channelId));
            if (captureId < 1) throw new ArgumentOutOfRangeException(nameof(captureId));
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source identity is required.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("A scope identity is required.", nameof(scopeId));
            if (epoch < 0) throw new ArgumentOutOfRangeException(nameof(epoch));
            ChannelId = channelId;
            CaptureId = captureId;
            SourceId = sourceId;
            ScopeId = scopeId;
            Epoch = epoch;
        }

        public Guid ChannelId { get; }
        public long CaptureId { get; }
        public string SourceId { get; }
        public string ScopeId { get; }
        public long Epoch { get; }

        public bool Equals(ObservationReference other)
        {
            return ChannelId == other.ChannelId && CaptureId == other.CaptureId && Epoch == other.Epoch && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) && string.Equals(ScopeId, other.ScopeId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ObservationReference && Equals((ObservationReference)obj);
        }

        public override int GetHashCode()
        {
            int identityHash = HashCode.Combine(ChannelId, CaptureId, Epoch);
            int sourceHash = StringComparer.Ordinal.GetHashCode(SourceId);
            int scopeHash = StringComparer.Ordinal.GetHashCode(ScopeId);
            return HashCode.Combine(identityHash, sourceHash, scopeHash);
        }

        public static bool operator ==(ObservationReference left, ObservationReference right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ObservationReference left, ObservationReference right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class CaptureFailure
    {
        public CaptureFailure(CaptureMetadata metadata, string code, string detail)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A failure code is required.", nameof(code));
            Code = code;
            Detail = detail ?? string.Empty;
        }

        public CaptureMetadata Metadata { get; }
        public string Code { get; }
        public string Detail { get; }
    }

    public enum ObservationReadState
    {
        Empty,
        Available,
        CaptureFailed,
        Evicted,
        ForeignReference
    }

    public sealed class ObservationRead<TObservation>
    {
        internal ObservationRead(ObservationReadState state, ObservationReference reference, TObservation observation, CaptureMetadata metadata, CaptureFailure failure)
        {
            State = state;
            Reference = reference;
            Observation = observation;
            Metadata = metadata;
            Failure = failure;
        }

        public ObservationReadState State { get; }
        public ObservationReference Reference { get; }
        public TObservation Observation { get; }
        public CaptureMetadata Metadata { get; }
        public CaptureFailure Failure { get; }
        public bool HasObservation
        {
            get { return State == ObservationReadState.Available; }
        }
    }

    public interface IObservationReader<TObservation>
    {
        ObservationRead<TObservation> ReadLatest();
        ObservationRead<TObservation> Read(ObservationReference reference);
    }

    public interface IObservationPublisher<TObservation>
    {
        ObservationReference Publish(TObservation observation, CaptureMetadata metadata);
        void ReportCaptureFailure(CaptureFailure failure);
    }
}
