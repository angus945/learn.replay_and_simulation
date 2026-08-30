using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using TraceBuffering;

namespace Testability
{
    public sealed class TraceRecorder
    {
        private readonly TraceBuffer<TraceEntry> buffer;
        public TraceRecorder(int capacity)
        {
            buffer = new TraceBuffer<TraceEntry>(capacity);
        }
        public ITraceReader<TraceEntry> Reader => buffer.Reader;
        public ITraceWriter<TraceEntry> Writer => buffer.Writer;
        public long DroppedCount => buffer.OverwrittenCount;
        public void Record(TraceEntry entry) => buffer.Writer.Record(entry);
        public IReadOnlyList<TraceEntry> Snapshot()
        {
            TraceBatch<TraceEntry> batch = buffer.Reader.Read(default, buffer.Capacity);
            TraceEntry[] copy = new TraceEntry[batch.Items.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = batch.Items[i].Entry;
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>Caller provides canonical bytes; never use object.GetHashCode for state comparison.</summary>
    public static class StateDigest
    {
        public static string Compute(byte[] canonicalBytes)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(canonicalBytes)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>In-process artifact persistence, not a remote protocol. Caller owns stream and storage policy.</summary>
    public static class ArtifactJson
    {
        public static void Write<T>(Stream destination, T artifact)
            => new DataContractJsonSerializer(typeof(T)).WriteObject(destination, artifact);
        public static T Read<T>(Stream source)
            => (T)new DataContractJsonSerializer(typeof(T)).ReadObject(source);
    }
}
