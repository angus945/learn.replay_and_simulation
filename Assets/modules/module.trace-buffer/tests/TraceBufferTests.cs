using System;
using NUnit.Framework;

namespace TraceBuffering.Tests
{
    public sealed class TraceBufferTests
    {
        [Test]
        public void PagesAdvanceExclusiveCursorWithoutDuplicates()
        {
            TraceBuffer<string> buffer = new TraceBuffer<string>(4);
            buffer.Writer.Record("a"); buffer.Writer.Record("b"); buffer.Writer.Record("c");
            TraceBatch<string> first = buffer.Reader.Read(default, 2);
            Assert.That(first.Items[0].Sequence, Is.EqualTo(1));
            Assert.That(first.Items[1].Entry, Is.EqualTo("b"));
            Assert.That(first.HasMore, Is.True);
            TraceBatch<string> second = buffer.Reader.Read(first.NextCursor, 2);
            Assert.That(second.Items.Count, Is.EqualTo(1));
            Assert.That(second.Items[0].Sequence, Is.EqualTo(3));
            Assert.That(second.HasMore, Is.False);
            Assert.That(buffer.Reader.Read(second.NextCursor, 2).Items, Is.Empty);
        }

        [Test]
        public void SlowReaderReceivesExactGapAndOnlyRetainedRecords()
        {
            TraceBuffer<int> buffer = new TraceBuffer<int>(2);
            buffer.Writer.Record(1);
            TraceCursor cursor = buffer.Reader.Read(default, 1).NextCursor;
            for (int i = 2; i <= 5; i++) buffer.Writer.Record(i);
            TraceBatch<int> batch = buffer.Reader.Read(cursor, 100);
            Assert.That(batch.MissedCount, Is.EqualTo(2));
            Assert.That(batch.OverwrittenCount, Is.EqualTo(3));
            Assert.That(batch.Items[0].Entry, Is.EqualTo(4));
            Assert.That(batch.Items[1].Entry, Is.EqualTo(5));
            Assert.That(buffer.Reader.Read(batch.NextCursor, 2).MissedCount, Is.Zero);
            Assert.That(buffer.Reader.Read(default, 2).MissedCount, Is.EqualTo(3));
        }

        [Test]
        public void NewStreamDetectsForeignCursorAndBeginsOwnHistory()
        {
            TraceBuffer<int> old = new TraceBuffer<int>(2);
            old.Writer.Record(1); old.Writer.Record(2);
            TraceCursor cursor = old.Reader.Read(default, 2).NextCursor;
            TraceBuffer<int> next = new TraceBuffer<int>(2);
            next.Writer.Record(9);
            TraceBatch<int> batch = next.Reader.Read(cursor, 2);
            Assert.That(batch.StreamChanged, Is.True);
            Assert.That(batch.Items[0].Sequence, Is.EqualTo(1));
            Assert.That(batch.Items[0].Entry, Is.EqualTo(9));
            Assert.That(batch.NextCursor.StreamId, Is.Not.EqualTo(cursor.StreamId));
        }

        [Test]
        public void SnapshotSurvivesOverwriteAndReadersHaveIndependentPositions()
        {
            TraceBuffer<int> buffer = new TraceBuffer<int>(1);
            buffer.Writer.Record(10);
            TraceBatch<int> first = buffer.Reader.Read(default, 1);
            buffer.Writer.Record(20);
            Assert.That(first.Items[0].Entry, Is.EqualTo(10));
            Assert.That(buffer.Reader.Read(first.NextCursor, 1).Items[0].Entry, Is.EqualTo(20));
            Assert.That(buffer.Reader.Read(default, 1).Items[0].Entry, Is.EqualTo(20));
        }

        [Test]
        public void ReaderCannotBeCastToWriterOrOwner()
        {
            TraceBuffer<string> buffer = new TraceBuffer<string>(1);
            Assert.That(buffer.Reader, Is.Not.InstanceOf<ITraceWriter<string>>());
            Assert.That(buffer.Reader, Is.Not.InstanceOf<TraceBuffer<string>>());
            Assert.That(buffer.Writer, Is.Not.InstanceOf<ITraceReader<string>>());
        }

        [Test]
        public void InvalidReadsDoNotConsumeAnything()
        {
            TraceBuffer<string> buffer = new TraceBuffer<string>(2);
            buffer.Writer.Record("a");
            TraceCursor cursor = buffer.Reader.Read(default, 1).NextCursor;
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Reader.Read(cursor, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Reader.Read(new TraceCursor(cursor.StreamId, 99), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TraceCursor(Guid.Empty, 1));
            Assert.Throws<ArgumentNullException>(() => buffer.Writer.Record(null));
            Assert.That(buffer.Reader.Read(default, 2).Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void EmptyStreamHasStableCursorAndNoFalseGap()
        {
            TraceBuffer<int> buffer = new TraceBuffer<int>(1);
            TraceBatch<int> empty = buffer.Reader.Read(default, 1);
            Assert.That(empty.Items, Is.Empty);
            Assert.That(empty.MissedCount, Is.Zero);
            Assert.That(empty.NextCursor.AfterSequence, Is.Zero);
            buffer.Writer.Record(5);
            Assert.That(buffer.Reader.Read(empty.NextCursor, 1).Items[0].Entry, Is.EqualTo(5));
        }
    }
}
