using System;
using InvariantChecks;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Testability.Tests
{
    public sealed class DiagnosticsTests
    {
        [Test]
        public void TraceIsBoundedOrderedAndSnapshotIsOwned()
        {
            TraceRecorder trace = new TraceRecorder(2);
            trace.Record(new TraceEntry("s", 1, 1, "a", "t", "ok"));
            IReadOnlyList<TraceEntry> before = trace.Snapshot();
            trace.Record(new TraceEntry("s", 2, 2, "a", "t", "ok"));
            trace.Record(new TraceEntry("s", 3, 3, "a", "t", "ok"));
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(before[0].Tick, Is.EqualTo(1));
            Assert.That(trace.Snapshot()[0].Tick, Is.EqualTo(2));
            Assert.That(trace.DroppedCount, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TraceRecorder(0));
        }

        [Test]
        public void InvariantsRequireSealRejectDuplicatesAndUseOrdinalOrder()
        {
            InvariantRegistry<int> registry = new InvariantRegistry<int>();
            registry.Register(new Check("z"));
            registry.Register(new Check("a"));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Check("a")));
            Assert.Throws<InvalidOperationException>(() => registry.Evaluate(1));
            registry.Seal();
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Check("b")));
            Assert.That(registry.Evaluate(1)[0].Code, Is.EqualTo("a"));
        }

        [Test]
        public void ArtifactCodecRoundTripsEscapedTextWithoutClosingStreams()
        {
            TraceEntry entry = new TraceEntry("s", 2, 4, "\"stage\"", "type", "換行\n\\text");
            using (MemoryStream stream = new MemoryStream())
            {
                ArtifactJson.Write(stream, entry);
                stream.Position = 0;
                TraceEntry copy = ArtifactJson.Read<TraceEntry>(stream);
                Assert.That(copy.Code, Is.EqualTo(entry.Code));
                Assert.That(copy.Stage, Is.EqualTo(entry.Stage));
                Assert.That(stream.CanRead, Is.True);
            }
        }

        private sealed class Check : IInvariant<int>
        {
            public Check(string code) { Code = code; }
            public string Code { get; }
            public InvariantViolation Evaluate(int value) => new InvariantViolation(Code, "test");
        }
    }
}
