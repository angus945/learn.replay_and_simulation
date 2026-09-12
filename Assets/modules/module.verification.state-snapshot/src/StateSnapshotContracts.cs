using System;

namespace Module.Verification.StateSnapshot
{
    public sealed class StateSnapshotCaptureMetadata
    {
        public StateSnapshotCaptureMetadata(string sourceId, string scopeId, long epoch, string correlationId = null)
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

    public readonly struct StateSnapshotReference : IEquatable<StateSnapshotReference>
    {
        public StateSnapshotReference(Guid channelId, long captureId, string sourceId, string scopeId, long epoch)
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

        public bool Equals(StateSnapshotReference other)
        {
            return ChannelId == other.ChannelId && CaptureId == other.CaptureId && Epoch == other.Epoch && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) && string.Equals(ScopeId, other.ScopeId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StateSnapshotReference && Equals((StateSnapshotReference)obj);
        }

        public override int GetHashCode()
        {
            int identityHash = HashCode.Combine(ChannelId, CaptureId, Epoch);
            int sourceHash = StringComparer.Ordinal.GetHashCode(SourceId);
            int scopeHash = StringComparer.Ordinal.GetHashCode(ScopeId);
            return HashCode.Combine(identityHash, sourceHash, scopeHash);
        }

        public static bool operator ==(StateSnapshotReference left, StateSnapshotReference right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateSnapshotReference left, StateSnapshotReference right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class StateSnapshotCaptureFailure
    {
        public StateSnapshotCaptureFailure(StateSnapshotCaptureMetadata metadata, string code, string detail)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A failure code is required.", nameof(code));
            Code = code;
            Detail = detail ?? string.Empty;
        }

        public StateSnapshotCaptureMetadata Metadata { get; }
        public string Code { get; }
        public string Detail { get; }
    }

    public enum StateSnapshotReadState
    {
        Empty,
        Available,
        CaptureFailed,
        Evicted,
        ForeignReference
    }

    public sealed class StateSnapshotRead<TSnapshot>
    {
        internal StateSnapshotRead(StateSnapshotReadState state, StateSnapshotReference reference, TSnapshot snapshot, StateSnapshotCaptureMetadata metadata, StateSnapshotCaptureFailure failure)
        {
            State = state;
            Reference = reference;
            Snapshot = snapshot;
            Metadata = metadata;
            Failure = failure;
        }

        public StateSnapshotReadState State { get; }
        public StateSnapshotReference Reference { get; }
        public TSnapshot Snapshot { get; }
        public StateSnapshotCaptureMetadata Metadata { get; }
        public StateSnapshotCaptureFailure Failure { get; }
        public bool HasSnapshot
        {
            get { return State == StateSnapshotReadState.Available; }
        }
    }

    public interface IStateSnapshotReader<TSnapshot>
    {
        StateSnapshotRead<TSnapshot> ReadLatest();
        StateSnapshotRead<TSnapshot> Read(StateSnapshotReference reference);
    }

    public interface IStateSnapshotPublisher<TSnapshot>
    {
        StateSnapshotReference Publish(TSnapshot snapshot, StateSnapshotCaptureMetadata metadata);
        void ReportStateSnapshotCaptureFailure(StateSnapshotCaptureFailure failure);
    }
}
