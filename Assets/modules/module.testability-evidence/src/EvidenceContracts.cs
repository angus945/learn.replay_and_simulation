using System;
using System.Collections.Generic;

namespace TestabilityEvidence
{
    public sealed class EvidenceManifest
    {
        public EvidenceManifest(string runId, string caseId, string buildId = null, string fixtureId = null)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("A run identity is required.", nameof(runId));
            if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("A case identity is required.", nameof(caseId));
            RunId = runId;
            CaseId = caseId;
            BuildId = buildId;
            FixtureId = fixtureId;
        }

        public string RunId { get; }
        public string CaseId { get; }
        public string BuildId { get; }
        public string FixtureId { get; }
    }

    public readonly struct EvidenceReference
    {
        public EvidenceReference(string scheme, string value)
        {
            if (string.IsNullOrWhiteSpace(scheme)) throw new ArgumentException("A reference scheme is required.", nameof(scheme));
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A reference value is required.", nameof(value));
            Scheme = scheme;
            Value = value;
        }

        public string Scheme { get; }
        public string Value { get; }
    }

    public enum EvidenceKind
    {
        Operation,
        Observation,
        Evaluation,
        Trace,
        Diagnostic,
        Attachment
    }

    public sealed class EvidenceEntry
    {
        public EvidenceEntry(EvidenceKind kind, string name, EvidenceReference reference, long estimatedBytes, bool truncated = false, bool redacted = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An evidence name is required.", nameof(name));
            if (estimatedBytes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedBytes));
            Kind = kind;
            Name = name;
            Reference = reference;
            EstimatedBytes = estimatedBytes;
            Truncated = truncated;
            Redacted = redacted;
        }

        public EvidenceKind Kind { get; }
        public string Name { get; }
        public EvidenceReference Reference { get; }
        public long EstimatedBytes { get; }
        public bool Truncated { get; }
        public bool Redacted { get; }
    }

    public sealed class EvidenceBundle
    {
        public EvidenceBundle(EvidenceManifest manifest, IEnumerable<EvidenceEntry> entries, long retainedBytes, int droppedEntryCount, string firstFailure, IEnumerable<string> cleanupErrors)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Entries = new List<EvidenceEntry>(entries ?? throw new ArgumentNullException(nameof(entries))).AsReadOnly();
            if (retainedBytes < 0) throw new ArgumentOutOfRangeException(nameof(retainedBytes));
            if (droppedEntryCount < 0) throw new ArgumentOutOfRangeException(nameof(droppedEntryCount));
            RetainedBytes = retainedBytes;
            DroppedEntryCount = droppedEntryCount;
            FirstFailure = firstFailure;
            CleanupErrors = new List<string>(cleanupErrors ?? throw new ArgumentNullException(nameof(cleanupErrors))).AsReadOnly();
        }

        public EvidenceManifest Manifest { get; }
        public IReadOnlyList<EvidenceEntry> Entries { get; }
        public long RetainedBytes { get; }
        public int DroppedEntryCount { get; }
        public string FirstFailure { get; }
        public IReadOnlyList<string> CleanupErrors { get; }
    }

    public sealed class EvidenceWriteResult
    {
        public EvidenceWriteResult(bool written, EvidenceReference reference, string code)
        {
            Written = written;
            Reference = reference;
            Code = code ?? string.Empty;
        }

        public bool Written { get; }
        public EvidenceReference Reference { get; }
        public string Code { get; }
    }

    public interface IEvidenceSink
    {
        EvidenceWriteResult Write(EvidenceBundle bundle);
    }
}
